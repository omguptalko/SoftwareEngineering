using Dapper;
using HIS.Application.Abstractions;
using HIS.Domain.Entities;
using Microsoft.Extensions.Configuration;
using HIS.Shared.Context;

namespace HIS.Infrastructure.Persistence;

public sealed class BillingRepository : IBillingRepository
{
    private readonly ITenantConnectionFactory _f;
    private readonly ITenantContext _tenant;
    public BillingRepository(ITenantConnectionFactory f, ITenantContext tenant) { _f = f; _tenant = tenant; }

    public async Task<Tariff?> GetTariffByCodeAsync(int branchId, string code, CancellationToken ct = default)
    {
        using var c = await _f.OpenMasterAsync(ct);   // Tariff master lives in the master DB
        // Prefer a branch-specific tariff, else fall back to the all-branches (NULL) row.
        return await c.QuerySingleOrDefaultAsync<Tariff>(new CommandDefinition(
            @"SELECT TOP 1 * FROM master.Tariff
              WHERE ServiceCode = @code AND IsActive = 1 AND (BranchId = @branchId OR BranchId IS NULL)
              ORDER BY CASE WHEN BranchId = @branchId THEN 0 ELSE 1 END",
            new { branchId, code }, cancellationToken: ct));
    }

    public async Task<string> NextBillNoAsync(int branchId, CancellationToken ct = default)
    {
        using var c = await _f.OpenDataAsync(ct);
        return await c.QuerySingleAsync<string>(new CommandDefinition(
            "EXEC [proc].usp_NextDocNo @BranchId=@branchId, @DocType='BILL', @Prefix='BILL', @FyCode=@fy",
            new { branchId, fy = _tenant.FiscalYearCode ?? "" }, cancellationToken: ct));
    }

    public async Task<long> CreateBillAsync(Bill bill, IReadOnlyList<BillLine> lines, CancellationToken ct = default)
    {
        using var c = await _f.OpenDataAsync(ct);
        using var tx = c.BeginTransaction();
        try
        {
            var billId = await c.QuerySingleAsync<long>(new CommandDefinition(
                @"INSERT INTO billing.Bill (BillNo, BranchId, PatientId, AdmissionId, CreatedUtc, GrossAmount, DiscountAmount, InsurancePays, PatientPays, Status)
                  VALUES (@BillNo, @BranchId, @PatientId, @AdmissionId, @CreatedUtc, @GrossAmount, @DiscountAmount, @InsurancePays, @PatientPays, @Status);
                  SELECT CAST(SCOPE_IDENTITY() AS BIGINT);", bill, tx, cancellationToken: ct));

            foreach (var l in lines)
                await c.ExecuteAsync(new CommandDefinition(
                    @"INSERT INTO billing.BillLine (BillId, TariffId, Description, Qty, Rate)
                      VALUES (@billId, @TariffId, @Description, @Qty, @Rate);",   // Amount is a computed column
                    new { billId, l.TariffId, l.Description, l.Qty, l.Rate }, tx, cancellationToken: ct));

            tx.Commit();
            return billId;
        }
        catch { tx.Rollback(); throw; }
    }

    public async Task<Bill?> GetBillAsync(long billId, CancellationToken ct = default)
    {
        using var c = await _f.OpenDataAsync(ct);
        return await c.QuerySingleOrDefaultAsync<Bill>(new CommandDefinition(
            "SELECT * FROM billing.Bill WHERE BillId = @billId", new { billId }, cancellationToken: ct));
    }

    public async Task<IReadOnlyList<(string, decimal, decimal, decimal)>> GetBillLinesAsync(long billId, CancellationToken ct = default)
    {
        using var c = await _f.OpenDataAsync(ct);
        var rows = await c.QueryAsync<(string, decimal, decimal, decimal)>(new CommandDefinition(
            "SELECT Description, Qty, Rate, Amount FROM billing.BillLine WHERE BillId = @billId ORDER BY LineId",
            new { billId }, cancellationToken: ct));
        return rows.ToList();
    }

    public async Task<IReadOnlyList<(long, string, string, decimal, decimal, decimal, string, DateTime)>> GetBillsAsync(int branchId, CancellationToken ct = default)
    {
        using var c = await _f.OpenDataAsync(ct);
        var rows = (await c.QueryAsync<(long BillId, string BillNo, long? PatientId, decimal Gross, decimal PatientPays, decimal Paid, string Status, DateTime CreatedUtc)>(new CommandDefinition(
            @"SELECT b.BillId, b.BillNo, b.PatientId, b.GrossAmount, b.PatientPays,
                     ISNULL((SELECT SUM(p.Amount) FROM billing.Payment p WHERE p.BillId = b.BillId AND p.Status = 'Captured'), 0) AS Paid,
                     b.Status, b.CreatedUtc
              FROM billing.Bill b WHERE b.BranchId = @branchId ORDER BY b.BillId DESC", new { branchId }, cancellationToken: ct))).ToList();
        var pats = await MasterLookup.PatientNamesAsync(_f, rows.Where(r => r.PatientId.HasValue).Select(r => r.PatientId!.Value), ct);
        return rows.Select(r => (r.BillId, r.BillNo,
            r.PatientId.HasValue ? pats.GetValueOrDefault(r.PatientId.Value, "") : "",
            r.Gross, r.PatientPays, r.Paid, r.Status, r.CreatedUtc)).ToList();
    }

    public async Task<long> InsertPaymentAsync(Payment p, CancellationToken ct = default)
    {
        using var c = await _f.OpenDataAsync(ct);
        const string sql = @"
INSERT INTO billing.Payment (BillId, PatientId, Mode, Gateway, Amount, GatewayRef, Status, CreatedUtc)
VALUES (@BillId, @PatientId, @Mode, @Gateway, @Amount, @GatewayRef, @Status, @CreatedUtc);
SELECT CAST(SCOPE_IDENTITY() AS BIGINT);";
        return await c.QuerySingleAsync<long>(new CommandDefinition(sql, p, cancellationToken: ct));
    }

    public async Task<decimal> GetPaidTotalAsync(long billId, CancellationToken ct = default)
    {
        using var c = await _f.OpenDataAsync(ct);
        return await c.ExecuteScalarAsync<decimal>(new CommandDefinition(
            "SELECT ISNULL(SUM(Amount),0) FROM billing.Payment WHERE BillId = @billId AND Status = 'Captured'",
            new { billId }, cancellationToken: ct));
    }

    public async Task UpdateBillStatusAsync(long billId, string status, CancellationToken ct = default)
    {
        using var c = await _f.OpenDataAsync(ct);
        await c.ExecuteAsync(new CommandDefinition(
            "UPDATE billing.Bill SET Status = @status WHERE BillId = @billId", new { billId, status }, cancellationToken: ct));
    }

    public async Task<long> InsertDepositAsync(PatientDeposit d, CancellationToken ct = default)
    {
        using var c = await _f.OpenDataAsync(ct);
        const string sql = @"
INSERT INTO billing.PatientDeposit (PatientId, Amount, Balance, CreatedUtc)
VALUES (@PatientId, @Amount, @Balance, @CreatedUtc);
SELECT CAST(SCOPE_IDENTITY() AS BIGINT);";
        return await c.QuerySingleAsync<long>(new CommandDefinition(sql, d, cancellationToken: ct));
    }

    public async Task<decimal> GetDepositBalanceAsync(long patientId, CancellationToken ct = default)
    {
        using var c = await _f.OpenDataAsync(ct);
        return await c.ExecuteScalarAsync<decimal>(new CommandDefinition(
            "SELECT ISNULL((SELECT TOP 1 Balance FROM billing.PatientDeposit WHERE PatientId = @patientId ORDER BY DepositId DESC), 0)",
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
