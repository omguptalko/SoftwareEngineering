using Dapper;
using HIS.Application.Abstractions;
using HIS.Domain.Entities;
using HIS.Shared.Context;

namespace HIS.Infrastructure.Persistence;

// L1.8.5 cutover: pharmacy dispensing/batches are fiscal-scoped (FY DB); the pending-Rx
// queue is clinical (master DB); Drug stock + Supplier are master. Dispense is therefore
// cross-plane: the authoritative batch deduction + dispense records commit atomically in the
// FY DB, then master Drug.StockQty / Prescription.Status are updated best-effort (a true-up
// job reconciles master stock; no distributed transaction — per D8/cross-plane note).
public sealed class PharmacyRepository : IPharmacyRepository
{
    private readonly ITenantConnectionFactory _f;
    public PharmacyRepository(ITenantConnectionFactory f) => _f = f;

    private sealed record BatchInfo(long BatchId, DateTime ExpiryDate, decimal Mrp, int QtyOnHand);

    public async Task<int?> GetDrugIdByCodeAsync(string code, CancellationToken ct = default)
    {
        using var c = await _f.OpenMasterAsync(ct);
        return await c.QuerySingleOrDefaultAsync<int?>(new CommandDefinition(
            "SELECT DrugId FROM master.Drug WHERE Code = @code AND IsActive = 1", new { code }, cancellationToken: ct));
    }

    public async Task<IReadOnlyList<(long, string, string, int, string)>> GetQueueAsync(int branchId, CancellationToken ct = default)
    {
        // Prescriptions/encounters/patients/doctors are all in the master DB → intra-DB join.
        using var c = await _f.OpenMasterAsync(ct);
        var rows = await c.QueryAsync<(long, string, string, int, string)>(new CommandDefinition(
            @"SELECT p.PrescriptionId,
                     ISNULL(pat.FullName,'') AS Patient,
                     ISNULL(doc.Name,'') AS Doctor,
                     (SELECT COUNT(1) FROM clinical.PrescriptionLine pl WHERE pl.PrescriptionId = p.PrescriptionId) AS Items,
                     p.Status
              FROM clinical.Prescription p
              INNER JOIN clinical.Encounter e ON e.EncounterId = p.EncounterId
              LEFT JOIN patient.Patient pat ON pat.PatientId = e.PatientId
              LEFT JOIN master.Doctor  doc ON doc.DoctorId = e.DoctorId
              WHERE e.BranchId = @branchId AND p.Status = 'Pending'
              ORDER BY p.PrescriptionId DESC", new { branchId }, cancellationToken: ct));
        return rows.ToList();
    }

    public async Task<IReadOnlyList<(string, string, decimal, int)>> GetBatchesAsync(int drugId, CancellationToken ct = default)
    {
        using var c = await _f.OpenDataAsync(ct);
        var rows = await c.QueryAsync<(string, string, decimal, int)>(new CommandDefinition(
            @"SELECT BatchNo, FORMAT(ExpiryDate,'MM/yy') AS Expiry, Mrp, QtyOnHand
              FROM pharmacy.DrugBatch WHERE DrugId = @drugId AND QtyOnHand > 0 ORDER BY ExpiryDate",
            new { drugId }, cancellationToken: ct));
        return rows.ToList();
    }

    public async Task<(long DispenseId, decimal Total)> DispenseAsync(
        Dispense dispense, IReadOnlyList<DispenseLineInput> lines, int expiryBlockDays, CancellationToken ct = default)
    {
        long dispenseId;
        decimal total = 0;
        var deducted = new List<(int DrugId, int Qty)>();

        // 1) Authoritative, atomic within the FY DB: validate batches, record dispense, deduct batch stock.
        using (var c = await _f.OpenDataAsync(ct))
        using (var tx = c.BeginTransaction())
        {
            try
            {
                var today = DateTime.UtcNow.Date;
                var resolved = new List<(long BatchId, int DrugId, int Qty, decimal Mrp)>();
                foreach (var l in lines)
                {
                    var b = await c.QuerySingleOrDefaultAsync<BatchInfo>(new CommandDefinition(
                        "SELECT BatchId, ExpiryDate, Mrp, QtyOnHand FROM pharmacy.DrugBatch WHERE DrugId = @DrugId AND BatchNo = @BatchNo",
                        new { l.DrugId, l.BatchNo }, tx, cancellationToken: ct));
                    if (b is null) throw new InvalidOperationException($"Unknown batch '{l.BatchNo}'.");
                    if (b.ExpiryDate.Date < today.AddDays(expiryBlockDays))
                        throw new InvalidOperationException($"Batch '{l.BatchNo}' is expired/near-expiry and cannot be dispensed.");
                    if (b.QtyOnHand < l.Qty)
                        throw new InvalidOperationException($"Insufficient stock for batch '{l.BatchNo}' (have {b.QtyOnHand}, need {l.Qty}).");
                    resolved.Add((b.BatchId, l.DrugId, l.Qty, b.Mrp));
                }

                dispenseId = await c.QuerySingleAsync<long>(new CommandDefinition(
                    @"INSERT INTO pharmacy.Dispense (PrescriptionId, BranchId, DispensedUtc, IsNdps)
                      VALUES (@PrescriptionId, @BranchId, @DispensedUtc, @IsNdps);
                      SELECT CAST(SCOPE_IDENTITY() AS BIGINT);", dispense, tx, cancellationToken: ct));

                foreach (var r in resolved)
                {
                    await c.ExecuteAsync(new CommandDefinition(
                        @"INSERT INTO pharmacy.DispenseLine (DispenseId, BatchId, Qty, UnitPrice) VALUES (@dispenseId, @BatchId, @Qty, @Mrp);",
                        new { dispenseId, r.BatchId, r.Qty, r.Mrp }, tx, cancellationToken: ct));
                    await c.ExecuteAsync(new CommandDefinition(
                        "UPDATE pharmacy.DrugBatch SET QtyOnHand = QtyOnHand - @Qty WHERE BatchId = @BatchId",
                        new { r.Qty, r.BatchId }, tx, cancellationToken: ct));
                    total += r.Qty * r.Mrp;
                    deducted.Add((r.DrugId, r.Qty));
                }
                tx.Commit();
            }
            catch { tx.Rollback(); throw; }
        }

        // 2) Best-effort master-plane sync (Drug aggregate stock + Rx status). A reconciliation
        //    job trues these up; a failure here must not undo the committed FY dispense.
        try
        {
            using var m = await _f.OpenMasterAsync(ct);
            foreach (var d in deducted)
                await m.ExecuteAsync(new CommandDefinition(
                    "UPDATE master.Drug SET StockQty = StockQty - @Qty WHERE DrugId = @DrugId",
                    new { d.Qty, d.DrugId }, cancellationToken: ct));
            if (dispense.PrescriptionId is long rxId)
                await m.ExecuteAsync(new CommandDefinition(
                    "UPDATE clinical.Prescription SET Status = 'Dispensed' WHERE PrescriptionId = @rxId",
                    new { rxId }, cancellationToken: ct));
        }
        catch { /* best-effort; FY dispense already authoritative */ }

        return (dispenseId, total);
    }
}

public sealed class InventoryRepository : IInventoryRepository
{
    private readonly ITenantConnectionFactory _f;
    private readonly ITenantContext _tenant;
    public InventoryRepository(ITenantConnectionFactory f, ITenantContext tenant) { _f = f; _tenant = tenant; }

    public async Task<IReadOnlyList<(string, string, int, int)>> GetLowStockAsync(CancellationToken ct = default)
    {
        using var c = await _f.OpenMasterAsync(ct);
        var rows = await c.QueryAsync<(string, string, int, int)>(new CommandDefinition(
            @"SELECT Code, Name, StockQty, ReorderLevel FROM master.Drug
              WHERE IsActive = 1 AND StockQty <= ReorderLevel ORDER BY StockQty", cancellationToken: ct));
        return rows.ToList();
    }

    public async Task<IReadOnlyList<(string, string, int, int)>> GetStockLevelsAsync(CancellationToken ct = default)
    {
        using var c = await _f.OpenMasterAsync(ct);
        var rows = await c.QueryAsync<(string, string, int, int)>(new CommandDefinition(
            @"SELECT Code, Name, StockQty, ReorderLevel FROM master.Drug
              WHERE IsActive = 1 ORDER BY (CAST(StockQty AS FLOAT) / NULLIF(ReorderLevel,0))", cancellationToken: ct));
        return rows.ToList();
    }

    public async Task<IReadOnlyList<Supplier>> GetSuppliersAsync(CancellationToken ct = default)
    {
        using var c = await _f.OpenMasterAsync(ct);
        return (await c.QueryAsync<Supplier>(new CommandDefinition(
            "SELECT SupplierId, Name, Gstin, IsActive FROM master.Supplier WHERE IsActive = 1 ORDER BY Name", cancellationToken: ct))).ToList();
    }

    public async Task<int> InsertSupplierAsync(string name, string? gstin, CancellationToken ct = default)
    {
        using var c = await _f.OpenMasterAsync(ct);
        // Reuse an active supplier of the same name (idempotent), else create it.
        const string sql = @"
DECLARE @id INT = (SELECT TOP 1 SupplierId FROM master.Supplier WHERE Name = @name AND IsActive = 1);
IF @id IS NULL
BEGIN
    INSERT master.Supplier (Name, Gstin, IsActive) VALUES (@name, @gstin, 1);
    SET @id = CAST(SCOPE_IDENTITY() AS INT);
END
SELECT @id;";
        return await c.QuerySingleAsync<int>(new CommandDefinition(sql, new { name, gstin }, cancellationToken: ct));
    }

    public async Task<string> NextPoNoAsync(int branchId, CancellationToken ct = default)
    {
        using var c = await _f.OpenDataAsync(ct);
        return await c.QuerySingleAsync<string>(new CommandDefinition(
            "EXEC [proc].usp_NextDocNo @BranchId=@branchId, @DocType='PO', @Prefix='PO', @FyCode=@fy",
            new { branchId, fy = _tenant.FiscalYearCode ?? "" }, cancellationToken: ct));
    }

    public async Task<long> CreatePoAsync(PurchaseOrder po, IReadOnlyList<PurchaseOrderLine> lines, CancellationToken ct = default)
    {
        using var c = await _f.OpenDataAsync(ct);
        using var tx = c.BeginTransaction();
        try
        {
            var poId = await c.QuerySingleAsync<long>(new CommandDefinition(
                @"INSERT INTO inventory.PurchaseOrder (PoNo, BranchId, SupplierId, CreatedUtc, Status)
                  VALUES (@PoNo, @BranchId, @SupplierId, @CreatedUtc, @Status);
                  SELECT CAST(SCOPE_IDENTITY() AS BIGINT);", po, tx, cancellationToken: ct));
            foreach (var l in lines)
                await c.ExecuteAsync(new CommandDefinition(
                    @"INSERT INTO inventory.PurchaseOrderLine (PoId, DrugId, ItemName, Qty, UnitPrice)
                      VALUES (@poId, @DrugId, @ItemName, @Qty, @UnitPrice);",
                    new { poId, l.DrugId, l.ItemName, l.Qty, l.UnitPrice }, tx, cancellationToken: ct));
            tx.Commit();
            return poId;
        }
        catch { tx.Rollback(); throw; }
    }

    public async Task<IReadOnlyList<(long, string, string?, int, decimal, string, DateTime)>> GetPurchaseOrdersAsync(int branchId, CancellationToken ct = default)
    {
        // POs + line aggregates live in the FY (data) DB; supplier names in master (D8 two-step).
        List<(long PoId, string PoNo, int SupplierId, string Status, DateTime CreatedUtc)> pos;
        Dictionary<long, (int Cnt, decimal Total)> agg;
        using (var c = await _f.OpenDataAsync(ct))
        {
            pos = (await c.QueryAsync<(long, string, int, string, DateTime)>(new CommandDefinition(
                "SELECT TOP 100 PoId, PoNo, SupplierId, Status, CreatedUtc FROM inventory.PurchaseOrder WHERE BranchId = @branchId ORDER BY PoId DESC",
                new { branchId }, cancellationToken: ct))).ToList();
            agg = (await c.QueryAsync<(long PoId, int Cnt, decimal Total)>(new CommandDefinition(
                "SELECT PoId, COUNT(*) AS Cnt, SUM(Qty * ISNULL(UnitPrice, 0)) AS Total FROM inventory.PurchaseOrderLine GROUP BY PoId",
                cancellationToken: ct))).ToDictionary(x => x.PoId, x => (x.Cnt, x.Total));
        }
        var ids = pos.Select(p => p.SupplierId).Distinct().ToArray();
        var names = new Dictionary<int, string>();
        if (ids.Length > 0)
            using (var m = await _f.OpenMasterAsync(ct))
                names = (await m.QueryAsync<(int SupplierId, string Name)>(new CommandDefinition(
                    "SELECT SupplierId, Name FROM master.Supplier WHERE SupplierId IN @ids", new { ids }, cancellationToken: ct)))
                    .ToDictionary(x => x.SupplierId, x => x.Name);
        return pos.Select(p =>
        {
            var a = agg.GetValueOrDefault(p.PoId);
            return (p.PoId, p.PoNo, (string?)names.GetValueOrDefault(p.SupplierId), a.Cnt, a.Total, p.Status, p.CreatedUtc);
        }).ToList();
    }

    public async Task<string?> GetPoStatusAsync(long poId, CancellationToken ct = default)
    {
        using var c = await _f.OpenDataAsync(ct);
        return await c.QuerySingleOrDefaultAsync<string?>(new CommandDefinition(
            "SELECT Status FROM inventory.PurchaseOrder WHERE PoId = @poId", new { poId }, cancellationToken: ct));
    }

    public async Task SetPoStatusAsync(long poId, string status, CancellationToken ct = default)
    {
        using var c = await _f.OpenDataAsync(ct);
        await c.ExecuteAsync(new CommandDefinition(
            "UPDATE inventory.PurchaseOrder SET Status = @status WHERE PoId = @poId", new { poId, status }, cancellationToken: ct));
    }

    public async Task<int> ReceivePoStockAsync(long poId, CancellationToken ct = default)
    {
        // Read the PO lines from the FY DB…
        List<(int? DrugId, string? ItemName, int Qty)> lines;
        using (var fy = await _f.OpenDataAsync(ct))
            lines = (await fy.QueryAsync<(int? DrugId, string? ItemName, int Qty)>(new CommandDefinition(
                "SELECT DrugId, ItemName, Qty FROM inventory.PurchaseOrderLine WHERE PoId = @poId", new { poId }, cancellationToken: ct))).ToList();
        if (lines.Count == 0) return 0;

        // …then add each matching drug's stock in the master DB (by DrugId, else by exact name).
        var received = 0;
        using var m = await _f.OpenMasterAsync(ct);
        foreach (var l in lines)
        {
            int? drugId = l.DrugId;
            if (drugId is null && !string.IsNullOrWhiteSpace(l.ItemName))
                drugId = await m.QuerySingleOrDefaultAsync<int?>(new CommandDefinition(
                    "SELECT TOP 1 DrugId FROM master.Drug WHERE Name = @name AND IsActive = 1", new { name = l.ItemName!.Trim() }, cancellationToken: ct));
            if (drugId is int did && l.Qty > 0)
            {
                await m.ExecuteAsync(new CommandDefinition(
                    "UPDATE master.Drug SET StockQty = StockQty + @qty WHERE DrugId = @did", new { qty = l.Qty, did }, cancellationToken: ct));
                received++;
            }
        }
        return received;
    }
}

public sealed class AssetRepository : IAssetRepository
{
    private readonly ITenantConnectionFactory _f;   // equipment register is longitudinal → master DB (D3)
    public AssetRepository(ITenantConnectionFactory f) => _f = f;

    public async Task<IReadOnlyList<Asset>> GetAssetsAsync(int branchId, CancellationToken ct = default)
    {
        using var c = await _f.OpenMasterAsync(ct);
        return (await c.QueryAsync<Asset>(new CommandDefinition(
            "SELECT * FROM master.Asset WHERE BranchId = @branchId ORDER BY Name", new { branchId }, cancellationToken: ct))).ToList();
    }

    public async Task<long> InsertAsync(Asset a, CancellationToken ct = default)
    {
        using var c = await _f.OpenMasterAsync(ct);
        const string sql = @"
INSERT INTO master.Asset (BranchId, AssetTag, Name, Category, AmcExpiry, NextMaintenance, Status)
VALUES (@BranchId, @AssetTag, @Name, @Category, @AmcExpiry, @NextMaintenance, @Status);
SELECT CAST(SCOPE_IDENTITY() AS BIGINT);";
        return await c.QuerySingleAsync<long>(new CommandDefinition(sql, a, cancellationToken: ct));
    }
}
