using Dapper;
using HIS.Application.Abstractions;
using HIS.Domain.Entities;
using Microsoft.Extensions.Configuration;

namespace HIS.Infrastructure.Persistence;

public sealed class BillingRepository : IBillingRepository
{
    private readonly IDbConnectionFactory _f;
    public BillingRepository(IDbConnectionFactory f) => _f = f;

    public async Task<Tariff?> GetTariffByCodeAsync(int branchId, string code, CancellationToken ct = default)
    {
        using var c = await _f.CreateOpenConnectionAsync(ct);
        // Prefer a branch-specific tariff, else fall back to the all-branches (NULL) row.
        return await c.QuerySingleOrDefaultAsync<Tariff>(new CommandDefinition(
            @"SELECT TOP 1 * FROM dbo.Tariff
              WHERE ServiceCode = @code AND IsActive = 1 AND (BranchId = @branchId OR BranchId IS NULL)
              ORDER BY CASE WHEN BranchId = @branchId THEN 0 ELSE 1 END",
            new { branchId, code }, cancellationToken: ct));
    }

    public async Task<string> NextBillNoAsync(int branchId, CancellationToken ct = default)
    {
        using var c = await _f.CreateOpenConnectionAsync(ct);
        return await c.QuerySingleAsync<string>(new CommandDefinition(
            "EXEC dbo.usp_NextDocNo @BranchId=@branchId, @DocType='BILL', @Prefix='BILL'", new { branchId }, cancellationToken: ct));
    }

    public async Task<long> CreateBillAsync(Bill bill, IReadOnlyList<BillLine> lines, CancellationToken ct = default)
    {
        using var c = await _f.CreateOpenConnectionAsync(ct);
        using var tx = c.BeginTransaction();
        try
        {
            var billId = await c.QuerySingleAsync<long>(new CommandDefinition(
                @"INSERT INTO dbo.Bill (BillNo, BranchId, PatientId, AdmissionId, CreatedUtc, GrossAmount, DiscountAmount, InsurancePays, PatientPays, Status)
                  VALUES (@BillNo, @BranchId, @PatientId, @AdmissionId, @CreatedUtc, @GrossAmount, @DiscountAmount, @InsurancePays, @PatientPays, @Status);
                  SELECT CAST(SCOPE_IDENTITY() AS BIGINT);", bill, tx, cancellationToken: ct));

            foreach (var l in lines)
                await c.ExecuteAsync(new CommandDefinition(
                    @"INSERT INTO dbo.BillLine (BillId, TariffId, Description, Qty, Rate)
                      VALUES (@billId, @TariffId, @Description, @Qty, @Rate);",   // Amount is a computed column
                    new { billId, l.TariffId, l.Description, l.Qty, l.Rate }, tx, cancellationToken: ct));

            tx.Commit();
            return billId;
        }
        catch { tx.Rollback(); throw; }
    }

    public async Task<Bill?> GetBillAsync(long billId, CancellationToken ct = default)
    {
        using var c = await _f.CreateOpenConnectionAsync(ct);
        return await c.QuerySingleOrDefaultAsync<Bill>(new CommandDefinition(
            "SELECT * FROM dbo.Bill WHERE BillId = @billId", new { billId }, cancellationToken: ct));
    }

    public async Task<IReadOnlyList<(string, decimal, decimal, decimal)>> GetBillLinesAsync(long billId, CancellationToken ct = default)
    {
        using var c = await _f.CreateOpenConnectionAsync(ct);
        var rows = await c.QueryAsync<(string, decimal, decimal, decimal)>(new CommandDefinition(
            "SELECT Description, Qty, Rate, Amount FROM dbo.BillLine WHERE BillId = @billId ORDER BY LineId",
            new { billId }, cancellationToken: ct));
        return rows.ToList();
    }

    public async Task<long> InsertPaymentAsync(Payment p, CancellationToken ct = default)
    {
        using var c = await _f.CreateOpenConnectionAsync(ct);
        const string sql = @"
INSERT INTO dbo.Payment (BillId, PatientId, Mode, Gateway, Amount, GatewayRef, Status, CreatedUtc)
VALUES (@BillId, @PatientId, @Mode, @Gateway, @Amount, @GatewayRef, @Status, @CreatedUtc);
SELECT CAST(SCOPE_IDENTITY() AS BIGINT);";
        return await c.QuerySingleAsync<long>(new CommandDefinition(sql, p, cancellationToken: ct));
    }

    public async Task<decimal> GetPaidTotalAsync(long billId, CancellationToken ct = default)
    {
        using var c = await _f.CreateOpenConnectionAsync(ct);
        return await c.ExecuteScalarAsync<decimal>(new CommandDefinition(
            "SELECT ISNULL(SUM(Amount),0) FROM dbo.Payment WHERE BillId = @billId AND Status = 'Captured'",
            new { billId }, cancellationToken: ct));
    }

    public async Task UpdateBillStatusAsync(long billId, string status, CancellationToken ct = default)
    {
        using var c = await _f.CreateOpenConnectionAsync(ct);
        await c.ExecuteAsync(new CommandDefinition(
            "UPDATE dbo.Bill SET Status = @status WHERE BillId = @billId", new { billId, status }, cancellationToken: ct));
    }

    public async Task<long> InsertDepositAsync(PatientDeposit d, CancellationToken ct = default)
    {
        using var c = await _f.CreateOpenConnectionAsync(ct);
        const string sql = @"
INSERT INTO dbo.PatientDeposit (PatientId, Amount, Balance, CreatedUtc)
VALUES (@PatientId, @Amount, @Balance, @CreatedUtc);
SELECT CAST(SCOPE_IDENTITY() AS BIGINT);";
        return await c.QuerySingleAsync<long>(new CommandDefinition(sql, d, cancellationToken: ct));
    }

    public async Task<decimal> GetDepositBalanceAsync(long patientId, CancellationToken ct = default)
    {
        using var c = await _f.CreateOpenConnectionAsync(ct);
        return await c.ExecuteScalarAsync<decimal>(new CommandDefinition(
            "SELECT ISNULL((SELECT TOP 1 Balance FROM dbo.PatientDeposit WHERE PatientId = @patientId ORDER BY DepositId DESC), 0)",
            new { patientId }, cancellationToken: ct));
    }
}

/// <summary>
/// Sandbox payment gateway (SRS §5). The active provider name is read from config
/// (Payments:ActiveProvider). Real adapters would read provider keys from Key Vault;
/// switching provider is a config change, not a code change. No keys are hardcoded.
/// </summary>
public sealed class SandboxPaymentGateway : IPaymentGateway
{
    private readonly string _provider;
    public SandboxPaymentGateway(IConfiguration config)
        => _provider = config["Payments:ActiveProvider"] ?? "Sandbox";

    public Task<GatewayChargeResult> ChargeAsync(GatewayChargeRequest request, CancellationToken ct = default)
    {
        var prefix = _provider.Length >= 3 ? _provider[..3].ToUpperInvariant() : _provider.ToUpperInvariant();
        var reference = $"{prefix}-{Guid.NewGuid():N}"[..14];
        return Task.FromResult(new GatewayChargeResult(true, _provider, reference, "Captured"));
    }
}
