using Dapper;
using HIS.Application.Abstractions;
using HIS.Domain.Entities;
using HIS.Shared.Context;

namespace HIS.Infrastructure.Persistence;

// L1.8.5 cutover: insurance/scheme transactions are fiscal-scoped (FY DB); Payer/HbpPackage/
// SchemePackage are master. Payer/patient display names resolved app-side (D8 two-step).
public sealed class ClaimsRepository : IClaimsRepository
{
    private readonly ITenantConnectionFactory _f;
    private readonly ITenantContext _tenant;
    public ClaimsRepository(ITenantConnectionFactory f, ITenantContext tenant) { _f = f; _tenant = tenant; }

    public async Task<int?> GetPayerIdByCodeAsync(string code, CancellationToken ct = default)
    {
        using var c = await _f.OpenMasterAsync(ct);
        return await c.QuerySingleOrDefaultAsync<int?>(new CommandDefinition(
            "SELECT PayerId FROM master.Payer WHERE Code = @code AND IsActive = 1", new { code }, cancellationToken: ct));
    }

    public async Task<long> InsertPolicyAsync(InsurancePolicy p, CancellationToken ct = default)
    {
        using var c = await _f.OpenDataAsync(ct);
        const string sql = @"
INSERT INTO insurance.InsurancePolicy (PatientId, PayerId, PolicyNo, MemberId, SumInsured, AvailableBalance, RoomRentCapPerDay, CoPayPct, ValidTo)
VALUES (@PatientId, @PayerId, @PolicyNo, @MemberId, @SumInsured, @AvailableBalance, @RoomRentCapPerDay, @CoPayPct, @ValidTo);
SELECT CAST(SCOPE_IDENTITY() AS BIGINT);";
        return await c.QuerySingleAsync<long>(new CommandDefinition(sql, p, cancellationToken: ct));
    }

    public async Task<IReadOnlyList<(long, string, string?, decimal?, decimal?, decimal?, decimal?)>> GetPoliciesAsync(long patientId, CancellationToken ct = default)
    {
        using var c = await _f.OpenDataAsync(ct);
        var pols = (await c.QueryAsync<(long PolicyId, int PayerId, string? PolicyNo, decimal? SumInsured, decimal? AvailableBalance, decimal? RoomRentCapPerDay, decimal? CoPayPct)>(new CommandDefinition(
            @"SELECT PolicyId, PayerId, PolicyNo, SumInsured, AvailableBalance, RoomRentCapPerDay, CoPayPct
              FROM insurance.InsurancePolicy WHERE PatientId = @patientId ORDER BY PolicyId DESC", new { patientId }, cancellationToken: ct))).ToList();
        var payers = await MasterLookup.PayerNamesAsync(_f, pols.Select(p => p.PayerId), ct);
        return pols.Select(p => (p.PolicyId, payers.GetValueOrDefault(p.PayerId, ""), p.PolicyNo, p.SumInsured, p.AvailableBalance, p.RoomRentCapPerDay, p.CoPayPct)).ToList();
    }

    public async Task<string> NextClaimNoAsync(int branchId, CancellationToken ct = default)
    {
        using var c = await _f.OpenDataAsync(ct);
        return await c.QuerySingleAsync<string>(new CommandDefinition(
            "EXEC [proc].usp_NextDocNo @BranchId=@branchId, @DocType='CLM', @Prefix='CL', @FyCode=@fy",
            new { branchId, fy = _tenant.FiscalYearCode ?? "" }, cancellationToken: ct));
    }

    public async Task<long> InsertClaimAsync(Claim cl, CancellationToken ct = default)
    {
        using var c = await _f.OpenDataAsync(ct);
        const string sql = @"
INSERT INTO insurance.Claim (ClaimNo, BranchId, PatientId, PayerId, PolicyId, AdmissionId, Channel, ProvisionalIcd10, PreAuthAmount, ApprovedAmount, SettledAmount, Status, SubmittedUtc, TatDueUtc)
VALUES (@ClaimNo, @BranchId, @PatientId, @PayerId, @PolicyId, @AdmissionId, @Channel, @ProvisionalIcd10, @PreAuthAmount, @ApprovedAmount, @SettledAmount, @Status, @SubmittedUtc, @TatDueUtc);
SELECT CAST(SCOPE_IDENTITY() AS BIGINT);";
        return await c.QuerySingleAsync<long>(new CommandDefinition(sql, cl, cancellationToken: ct));
    }

    public async Task AddEventAsync(ClaimEvent e, CancellationToken ct = default)
    {
        using var c = await _f.OpenDataAsync(ct);
        await c.ExecuteAsync(new CommandDefinition(
            @"INSERT INTO insurance.ClaimEvent (ClaimId, EventType, Amount, Notes, OccurredUtc)
              VALUES (@ClaimId, @EventType, @Amount, @Notes, @OccurredUtc);", e, cancellationToken: ct));
    }

    public async Task AddDocumentAsync(ClaimDocument d, CancellationToken ct = default)
    {
        using var c = await _f.OpenDataAsync(ct);
        await c.ExecuteAsync(new CommandDefinition(
            @"INSERT INTO insurance.ClaimDocument (ClaimId, DocType, DocUrl, IsMandatory, Attached)
              VALUES (@ClaimId, @DocType, @DocUrl, @IsMandatory, @Attached);", d, cancellationToken: ct));
    }

    public async Task<Claim?> GetClaimAsync(long claimId, CancellationToken ct = default)
    {
        using var c = await _f.OpenDataAsync(ct);
        return await c.QuerySingleOrDefaultAsync<Claim>(new CommandDefinition(
            "SELECT * FROM insurance.Claim WHERE ClaimId = @claimId", new { claimId }, cancellationToken: ct));
    }

    public async Task UpdateClaimAsync(Claim cl, CancellationToken ct = default)
    {
        using var c = await _f.OpenDataAsync(ct);
        await c.ExecuteAsync(new CommandDefinition(
            @"UPDATE insurance.Claim SET Status = @Status, ApprovedAmount = @ApprovedAmount, SettledAmount = @SettledAmount
              WHERE ClaimId = @ClaimId", cl, cancellationToken: ct));
    }

    public async Task<IReadOnlyList<(long, string, decimal?, string?, DateTime)>> GetEventsAsync(long claimId, CancellationToken ct = default)
    {
        using var c = await _f.OpenDataAsync(ct);
        var rows = await c.QueryAsync<(long, string, decimal?, string?, DateTime)>(new CommandDefinition(
            "SELECT EventId, EventType, Amount, Notes, OccurredUtc FROM insurance.ClaimEvent WHERE ClaimId = @claimId ORDER BY EventId",
            new { claimId }, cancellationToken: ct));
        return rows.ToList();
    }

    public async Task<IReadOnlyList<(long, string, string, string, decimal?, decimal?, string)>> GetClaimsAsync(int branchId, CancellationToken ct = default)
    {
        using var c = await _f.OpenDataAsync(ct);
        var claims = (await c.QueryAsync<(long ClaimId, string ClaimNo, long PatientId, int PayerId, decimal? PreAuth, decimal? Approved, string Status)>(new CommandDefinition(
            @"SELECT ClaimId, ClaimNo, PatientId, PayerId, PreAuthAmount, ApprovedAmount, Status
              FROM insurance.Claim WHERE BranchId = @branchId ORDER BY ClaimId DESC", new { branchId }, cancellationToken: ct))).ToList();
        var pats = await MasterLookup.PatientNamesAsync(_f, claims.Select(c => c.PatientId), ct);
        var pays = await MasterLookup.PayerNamesAsync(_f, claims.Select(c => c.PayerId), ct);
        return claims.Select(c => (c.ClaimId, c.ClaimNo, pats.GetValueOrDefault(c.PatientId, ""), pays.GetValueOrDefault(c.PayerId, ""), c.PreAuth, c.Approved, c.Status)).ToList();
    }

    public async Task<IReadOnlyList<(string, int)>> GetStatusCountsAsync(int branchId, CancellationToken ct = default)
    {
        using var c = await _f.OpenDataAsync(ct);
        var rows = await c.QueryAsync<(string, int)>(new CommandDefinition(
            "SELECT Status, COUNT(1) FROM insurance.Claim WHERE BranchId = @branchId GROUP BY Status", new { branchId }, cancellationToken: ct));
        return rows.ToList();
    }

    public async Task<long> InsertReconciliationAsync(SettlementReconciliation r, CancellationToken ct = default)
    {
        using var c = await _f.OpenDataAsync(ct);
        const string sql = @"
INSERT INTO insurance.SettlementReconciliation (ClaimId, Utr, BankAmount, ReconciledUtc, Status)
VALUES (@ClaimId, @Utr, @BankAmount, @ReconciledUtc, @Status);
SELECT CAST(SCOPE_IDENTITY() AS BIGINT);";
        return await c.QuerySingleAsync<long>(new CommandDefinition(sql, r, cancellationToken: ct));
    }
}

public sealed class PmjayRepository : IPmjayRepository
{
    private readonly ITenantConnectionFactory _f;
    private readonly ITenantContext _tenant;
    public PmjayRepository(ITenantConnectionFactory f, ITenantContext tenant) { _f = f; _tenant = tenant; }

    private sealed record PkgRow(int PackageId, decimal Rate);

    public async Task<(int PackageId, decimal Rate)?> GetPackageByCodeAsync(string code, CancellationToken ct = default)
    {
        using var c = await _f.OpenMasterAsync(ct);
        var row = await c.QuerySingleOrDefaultAsync<PkgRow>(new CommandDefinition(
            "SELECT PackageId, Rate FROM master.HbpPackage WHERE Code = @code AND IsActive = 1", new { code }, cancellationToken: ct));
        return row is null ? null : (row.PackageId, row.Rate);
    }

    public async Task<long> UpsertBeneficiaryAsync(PmjayBeneficiary b, CancellationToken ct = default)
    {
        using var c = await _f.OpenDataAsync(ct);
        var existing = await c.QuerySingleOrDefaultAsync<long?>(new CommandDefinition(
            "SELECT BeneficiaryId FROM scheme.PmjayBeneficiary WHERE PatientId = @PatientId", b, cancellationToken: ct));
        if (existing is long id)
        {
            await c.ExecuteAsync(new CommandDefinition(
                @"UPDATE scheme.PmjayBeneficiary SET PmjayId = @PmjayId, BisVerified = @BisVerified, FamilyFloater = @FamilyFloater
                  WHERE BeneficiaryId = @id", new { b.PmjayId, b.BisVerified, b.FamilyFloater, id }, cancellationToken: ct));
            return id;
        }
        return await c.QuerySingleAsync<long>(new CommandDefinition(
            @"INSERT INTO scheme.PmjayBeneficiary (PatientId, PmjayId, BisVerified, FamilyFloater, UsedAmount)
              VALUES (@PatientId, @PmjayId, @BisVerified, @FamilyFloater, @UsedAmount);
              SELECT CAST(SCOPE_IDENTITY() AS BIGINT);", b, cancellationToken: ct));
    }

    public async Task<long> InsertCaseAsync(PmjayCase cse, CancellationToken ct = default)
    {
        using var c = await _f.OpenDataAsync(ct);
        const string sql = @"
INSERT INTO scheme.PmjayCase (ClaimId, PackageId, TmsCaseNo, AyushmanMitra, AadhaarDischargeVerified)
VALUES (@ClaimId, @PackageId, @TmsCaseNo, @AyushmanMitra, @AadhaarDischargeVerified);
SELECT CAST(SCOPE_IDENTITY() AS BIGINT);";
        return await c.QuerySingleAsync<long>(new CommandDefinition(sql, cse, cancellationToken: ct));
    }

    public async Task<string> NextTmsNoAsync(int branchId, CancellationToken ct = default)
    {
        using var c = await _f.OpenDataAsync(ct);
        return await c.QuerySingleAsync<string>(new CommandDefinition(
            "EXEC [proc].usp_NextDocNo @BranchId=@branchId, @DocType='TMS', @Prefix='TMS', @FyCode=@fy",
            new { branchId, fy = _tenant.FiscalYearCode ?? "" }, cancellationToken: ct));
    }
}

public sealed class SchemeRepository : ISchemeRepository
{
    private readonly ITenantConnectionFactory _f;
    public SchemeRepository(ITenantConnectionFactory f) => _f = f;

    public async Task<long> UpsertMembershipAsync(SchemeMembership m, CancellationToken ct = default)
    {
        using var c = await _f.OpenDataAsync(ct);
        var existing = await c.QuerySingleOrDefaultAsync<long?>(new CommandDefinition(
            "SELECT MembershipId FROM scheme.SchemeMembership WHERE PatientId = @PatientId AND SchemeType = @SchemeType", m, cancellationToken: ct));
        if (existing is long id)
        {
            await c.ExecuteAsync(new CommandDefinition(
                @"UPDATE scheme.SchemeMembership SET MemberNo = @MemberNo, SecondaryRef = @SecondaryRef, Verified = @Verified
                  WHERE MembershipId = @id", new { m.MemberNo, m.SecondaryRef, m.Verified, id }, cancellationToken: ct));
            return id;
        }
        return await c.QuerySingleAsync<long>(new CommandDefinition(
            @"INSERT INTO scheme.SchemeMembership (PatientId, SchemeType, MemberNo, SecondaryRef, Verified, ValidTo)
              VALUES (@PatientId, @SchemeType, @MemberNo, @SecondaryRef, @Verified, @ValidTo);
              SELECT CAST(SCOPE_IDENTITY() AS BIGINT);", m, cancellationToken: ct));
    }

    public async Task<IReadOnlyList<SchemePackage>> GetPackagesAsync(string schemeType, string? q, CancellationToken ct = default)
    {
        using var c = await _f.OpenMasterAsync(ct);   // scheme package rate master lives in master DB
        var like = "%" + (q ?? "").Trim() + "%";
        return (await c.QueryAsync<SchemePackage>(new CommandDefinition(
            @"SELECT SchemePackageId, SchemeType, Code, Name, Rate, IsActive FROM master.SchemePackage
              WHERE SchemeType = @schemeType AND IsActive = 1 AND (@like = '%%' OR Code LIKE @like OR Name LIKE @like)
              ORDER BY Name", new { schemeType, like }, cancellationToken: ct))).ToList();
    }
}
