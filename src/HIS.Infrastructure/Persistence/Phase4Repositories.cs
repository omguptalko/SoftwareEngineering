using Dapper;
using HIS.Application.Abstractions;
using HIS.Domain.Entities;

namespace HIS.Infrastructure.Persistence;

public sealed class PharmacyRepository : IPharmacyRepository
{
    private readonly IDbConnectionFactory _f;
    public PharmacyRepository(IDbConnectionFactory f) => _f = f;

    private sealed record BatchInfo(long BatchId, DateTime ExpiryDate, decimal Mrp, int QtyOnHand);

    public async Task<int?> GetDrugIdByCodeAsync(string code, CancellationToken ct = default)
    {
        using var c = await _f.CreateOpenConnectionAsync(ct);
        return await c.QuerySingleOrDefaultAsync<int?>(new CommandDefinition(
            "SELECT DrugId FROM dbo.Drug WHERE Code = @code AND IsActive = 1", new { code }, cancellationToken: ct));
    }

    public async Task<IReadOnlyList<(long, string, string, int, string)>> GetQueueAsync(int branchId, CancellationToken ct = default)
    {
        using var c = await _f.CreateOpenConnectionAsync(ct);
        var rows = await c.QueryAsync<(long, string, string, int, string)>(new CommandDefinition(
            @"SELECT p.PrescriptionId,
                     ISNULL(pat.FullName,'') AS Patient,
                     ISNULL(doc.Name,'') AS Doctor,
                     (SELECT COUNT(1) FROM dbo.PrescriptionLine pl WHERE pl.PrescriptionId = p.PrescriptionId) AS Items,
                     p.Status
              FROM dbo.Prescription p
              INNER JOIN dbo.Encounter e ON e.EncounterId = p.EncounterId
              LEFT JOIN dbo.Patient pat ON pat.PatientId = e.PatientId
              LEFT JOIN dbo.Doctor  doc ON doc.DoctorId = e.DoctorId
              WHERE e.BranchId = @branchId AND p.Status = 'Pending'
              ORDER BY p.PrescriptionId DESC", new { branchId }, cancellationToken: ct));
        return rows.ToList();
    }

    public async Task<IReadOnlyList<(string, string, decimal, int)>> GetBatchesAsync(int drugId, CancellationToken ct = default)
    {
        using var c = await _f.CreateOpenConnectionAsync(ct);
        var rows = await c.QueryAsync<(string, string, decimal, int)>(new CommandDefinition(
            @"SELECT BatchNo, FORMAT(ExpiryDate,'MM/yy') AS Expiry, Mrp, QtyOnHand
              FROM dbo.DrugBatch WHERE DrugId = @drugId AND QtyOnHand > 0 ORDER BY ExpiryDate",
            new { drugId }, cancellationToken: ct));
        return rows.ToList();
    }

    public async Task<(long DispenseId, decimal Total)> DispenseAsync(
        Dispense dispense, IReadOnlyList<DispenseLineInput> lines, int expiryBlockDays, CancellationToken ct = default)
    {
        using var c = await _f.CreateOpenConnectionAsync(ct);
        using var tx = c.BeginTransaction();
        try
        {
            var today = DateTime.UtcNow.Date;
            var resolved = new List<(long BatchId, int DrugId, int Qty, decimal Mrp)>();

            foreach (var l in lines)
            {
                var b = await c.QuerySingleOrDefaultAsync<BatchInfo>(new CommandDefinition(
                    "SELECT BatchId, ExpiryDate, Mrp, QtyOnHand FROM dbo.DrugBatch WHERE DrugId = @DrugId AND BatchNo = @BatchNo",
                    new { l.DrugId, l.BatchNo }, tx, cancellationToken: ct));

                if (b is null) throw new InvalidOperationException($"Unknown batch '{l.BatchNo}'.");
                if (b.ExpiryDate.Date < today.AddDays(expiryBlockDays))
                    throw new InvalidOperationException($"Batch '{l.BatchNo}' is expired/near-expiry and cannot be dispensed.");
                if (b.QtyOnHand < l.Qty)
                    throw new InvalidOperationException($"Insufficient stock for batch '{l.BatchNo}' (have {b.QtyOnHand}, need {l.Qty}).");

                resolved.Add((b.BatchId, l.DrugId, l.Qty, b.Mrp));
            }

            var dispenseId = await c.QuerySingleAsync<long>(new CommandDefinition(
                @"INSERT INTO dbo.Dispense (PrescriptionId, BranchId, DispensedUtc, IsNdps)
                  VALUES (@PrescriptionId, @BranchId, @DispensedUtc, @IsNdps);
                  SELECT CAST(SCOPE_IDENTITY() AS BIGINT);", dispense, tx, cancellationToken: ct));

            decimal total = 0;
            foreach (var r in resolved)
            {
                await c.ExecuteAsync(new CommandDefinition(
                    @"INSERT INTO dbo.DispenseLine (DispenseId, BatchId, Qty, UnitPrice) VALUES (@dispenseId, @BatchId, @Qty, @Mrp);",
                    new { dispenseId, r.BatchId, r.Qty, r.Mrp }, tx, cancellationToken: ct));
                await c.ExecuteAsync(new CommandDefinition(
                    "UPDATE dbo.DrugBatch SET QtyOnHand = QtyOnHand - @Qty WHERE BatchId = @BatchId",
                    new { r.Qty, r.BatchId }, tx, cancellationToken: ct));
                await c.ExecuteAsync(new CommandDefinition(
                    "UPDATE dbo.Drug SET StockQty = StockQty - @Qty WHERE DrugId = @DrugId",
                    new { r.Qty, r.DrugId }, tx, cancellationToken: ct));
                total += r.Qty * r.Mrp;
            }

            if (dispense.PrescriptionId is long rxId)
                await c.ExecuteAsync(new CommandDefinition(
                    "UPDATE dbo.Prescription SET Status = 'Dispensed' WHERE PrescriptionId = @rxId",
                    new { rxId }, tx, cancellationToken: ct));

            tx.Commit();
            return (dispenseId, total);
        }
        catch
        {
            tx.Rollback();
            throw;
        }
    }
}

public sealed class InventoryRepository : IInventoryRepository
{
    private readonly IDbConnectionFactory _f;
    public InventoryRepository(IDbConnectionFactory f) => _f = f;

    public async Task<IReadOnlyList<(string, string, int, int)>> GetLowStockAsync(CancellationToken ct = default)
    {
        using var c = await _f.CreateOpenConnectionAsync(ct);
        var rows = await c.QueryAsync<(string, string, int, int)>(new CommandDefinition(
            @"SELECT Code, Name, StockQty, ReorderLevel FROM dbo.Drug
              WHERE IsActive = 1 AND StockQty <= ReorderLevel ORDER BY StockQty", cancellationToken: ct));
        return rows.ToList();
    }

    public async Task<IReadOnlyList<Supplier>> GetSuppliersAsync(CancellationToken ct = default)
    {
        using var c = await _f.CreateOpenConnectionAsync(ct);
        return (await c.QueryAsync<Supplier>(new CommandDefinition(
            "SELECT SupplierId, Name, Gstin, IsActive FROM dbo.Supplier WHERE IsActive = 1 ORDER BY Name", cancellationToken: ct))).ToList();
    }

    public async Task<string> NextPoNoAsync(int branchId, CancellationToken ct = default)
    {
        using var c = await _f.CreateOpenConnectionAsync(ct);
        return await c.QuerySingleAsync<string>(new CommandDefinition(
            "EXEC dbo.usp_NextDocNo @BranchId=@branchId, @DocType='PO', @Prefix='PO'", new { branchId }, cancellationToken: ct));
    }

    public async Task<long> CreatePoAsync(PurchaseOrder po, IReadOnlyList<PurchaseOrderLine> lines, CancellationToken ct = default)
    {
        using var c = await _f.CreateOpenConnectionAsync(ct);
        using var tx = c.BeginTransaction();
        try
        {
            var poId = await c.QuerySingleAsync<long>(new CommandDefinition(
                @"INSERT INTO dbo.PurchaseOrder (PoNo, BranchId, SupplierId, CreatedUtc, Status)
                  VALUES (@PoNo, @BranchId, @SupplierId, @CreatedUtc, @Status);
                  SELECT CAST(SCOPE_IDENTITY() AS BIGINT);", po, tx, cancellationToken: ct));

            foreach (var l in lines)
                await c.ExecuteAsync(new CommandDefinition(
                    @"INSERT INTO dbo.PurchaseOrderLine (PoId, DrugId, ItemName, Qty, UnitPrice)
                      VALUES (@poId, @DrugId, @ItemName, @Qty, @UnitPrice);",
                    new { poId, l.DrugId, l.ItemName, l.Qty, l.UnitPrice }, tx, cancellationToken: ct));

            tx.Commit();
            return poId;
        }
        catch { tx.Rollback(); throw; }
    }
}

public sealed class AssetRepository : IAssetRepository
{
    private readonly IDbConnectionFactory _f;
    public AssetRepository(IDbConnectionFactory f) => _f = f;

    public async Task<IReadOnlyList<Asset>> GetAssetsAsync(int branchId, CancellationToken ct = default)
    {
        using var c = await _f.CreateOpenConnectionAsync(ct);
        return (await c.QueryAsync<Asset>(new CommandDefinition(
            "SELECT * FROM dbo.Asset WHERE BranchId = @branchId ORDER BY Name", new { branchId }, cancellationToken: ct))).ToList();
    }

    public async Task<long> InsertAsync(Asset a, CancellationToken ct = default)
    {
        using var c = await _f.CreateOpenConnectionAsync(ct);
        const string sql = @"
INSERT INTO dbo.Asset (BranchId, AssetTag, Name, Category, AmcExpiry, NextMaintenance, Status)
VALUES (@BranchId, @AssetTag, @Name, @Category, @AmcExpiry, @NextMaintenance, @Status);
SELECT CAST(SCOPE_IDENTITY() AS BIGINT);";
        return await c.QuerySingleAsync<long>(new CommandDefinition(sql, a, cancellationToken: ct));
    }
}
