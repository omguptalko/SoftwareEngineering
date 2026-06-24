using Dapper;
using HIS.Application.Abstractions;
using HIS.Domain.Entities;

namespace HIS.Infrastructure.Persistence;

public sealed class ClaimsRepository : IClaimsRepository
{
    private readonly IDbConnectionFactory _f;
    public ClaimsRepository(IDbConnectionFactory f) => _f = f;

    public async Task<int?> GetPayerIdByCodeAsync(string code, CancellationToken ct = default)
    {
        using var c = await _f.CreateOpenConnectionAsync(ct);
        return await c.QuerySingleOrDefaultAsync<int?>(new CommandDefinition(
            "SELECT PayerId FROM dbo.Payer WHERE Code = @code AND IsActive = 1", new { code }, cancellationToken: ct));
    }

    public async Task<long> InsertPolicyAsync(InsurancePolicy p, CancellationToken ct = default)
    {
        using var c = await _f.CreateOpenConnectionAsync(ct);
        const string sql = @"
INSERT INTO dbo.InsurancePolicy (PatientId, PayerId, PolicyNo, MemberId, SumInsured, AvailableBalance, RoomRentCapPerDay, CoPayPct, ValidTo)
VALUES (@PatientId, @PayerId, @PolicyNo, @MemberId, @SumInsured, @AvailableBalance, @RoomRentCapPerDay, @CoPayPct, @ValidTo);
SELECT CAST(SCOPE_IDENTITY() AS BIGINT);";
        return await c.QuerySingleAsync<long>(new CommandDefinition(sql, p, cancellationToken: ct));
    }

    public async Task<IReadOnlyList<(long, string, string?, decimal?, decimal?, decimal?, decimal?)>> GetPoliciesAsync(long patientId, CancellationToken ct = default)
    {
        using var c = await _f.CreateOpenConnectionAsync(ct);
        var rows = await c.QueryAsync<(long, string, string?, decimal?, decimal?, decimal?, decimal?)>(new CommandDefinition(
            @"SELECT p.PolicyId, py.Name AS Payer, p.PolicyNo, p.SumInsured, p.AvailableBalance, p.RoomRentCapPerDay, p.CoPayPct
              FROM dbo.InsurancePolicy p INNER JOIN dbo.Payer py ON py.PayerId = p.PayerId
              WHERE p.PatientId = @patientId ORDER BY p.PolicyId DESC", new { patientId }, cancellationToken: ct));
        return rows.ToList();
    }

    public async Task<string> NextClaimNoAsync(int branchId, CancellationToken ct = default)
    {
        using var c = await _f.CreateOpenConnectionAsync(ct);
        return await c.QuerySingleAsync<string>(new CommandDefinition(
            "EXEC dbo.usp_NextDocNo @BranchId=@branchId, @DocType='CLM', @Prefix='CL'", new { branchId }, cancellationToken: ct));
    }

    public async Task<long> InsertClaimAsync(Claim cl, CancellationToken ct = default)
    {
        using var c = await _f.CreateOpenConnectionAsync(ct);
        const string sql = @"
INSERT INTO dbo.Claim (ClaimNo, BranchId, PatientId, PayerId, PolicyId, AdmissionId, Channel, ProvisionalIcd10, PreAuthAmount, ApprovedAmount, SettledAmount, Status, SubmittedUtc, TatDueUtc)
VALUES (@ClaimNo, @BranchId, @PatientId, @PayerId, @PolicyId, @AdmissionId, @Channel, @ProvisionalIcd10, @PreAuthAmount, @ApprovedAmount, @SettledAmount, @Status, @SubmittedUtc, @TatDueUtc);
SELECT CAST(SCOPE_IDENTITY() AS BIGINT);";
        return await c.QuerySingleAsync<long>(new CommandDefinition(sql, cl, cancellationToken: ct));
    }

    public async Task AddEventAsync(ClaimEvent e, CancellationToken ct = default)
    {
        using var c = await _f.CreateOpenConnectionAsync(ct);
        await c.ExecuteAsync(new CommandDefinition(
            @"INSERT INTO dbo.ClaimEvent (ClaimId, EventType, Amount, Notes, OccurredUtc)
              VALUES (@ClaimId, @EventType, @Amount, @Notes, @OccurredUtc);", e, cancellationToken: ct));
    }

    public async Task AddDocumentAsync(ClaimDocument d, CancellationToken ct = default)
    {
        using var c = await _f.CreateOpenConnectionAsync(ct);
        await c.ExecuteAsync(new CommandDefinition(
            @"INSERT INTO dbo.ClaimDocument (ClaimId, DocType, DocUrl, IsMandatory, Attached)
              VALUES (@ClaimId, @DocType, @DocUrl, @IsMandatory, @Attached);", d, cancellationToken: ct));
    }

    public async Task<Claim?> GetClaimAsync(long claimId, CancellationToken ct = default)
    {
        using var c = await _f.CreateOpenConnectionAsync(ct);
        return await c.QuerySingleOrDefaultAsync<Claim>(new CommandDefinition(
            "SELECT * FROM dbo.Claim WHERE ClaimId = @claimId", new { claimId }, cancellationToken: ct));
    }

    public async Task UpdateClaimAsync(Claim cl, CancellationToken ct = default)
    {
        using var c = await _f.CreateOpenConnectionAsync(ct);
        await c.ExecuteAsync(new CommandDefinition(
            @"UPDATE dbo.Claim SET Status = @Status, ApprovedAmount = @ApprovedAmount, SettledAmount = @SettledAmount
              WHERE ClaimId = @ClaimId", cl, cancellationToken: ct));
    }

    public async Task<IReadOnlyList<(long, string, decimal?, string?, DateTime)>> GetEventsAsync(long claimId, CancellationToken ct = default)
    {
        using var c = await _f.CreateOpenConnectionAsync(ct);
        var rows = await c.QueryAsync<(long, string, decimal?, string?, DateTime)>(new CommandDefinition(
            "SELECT EventId, EventType, Amount, Notes, OccurredUtc FROM dbo.ClaimEvent WHERE ClaimId = @claimId ORDER BY EventId",
            new { claimId }, cancellationToken: ct));
        return rows.ToList();
    }

    public async Task<IReadOnlyList<(long, string, string, string, decimal?, decimal?, string)>> GetClaimsAsync(int branchId, CancellationToken ct = default)
    {
        using var c = await _f.CreateOpenConnectionAsync(ct);
        var rows = await c.QueryAsync<(long, string, string, string, decimal?, decimal?, string)>(new CommandDefinition(
            @"SELECT c.ClaimId, c.ClaimNo, ISNULL(pat.FullName,'') AS Patient, ISNULL(py.Name,'') AS Payer,
                     c.PreAuthAmount, c.ApprovedAmount, c.Status
              FROM dbo.Claim c
              LEFT JOIN dbo.Patient pat ON pat.PatientId = c.PatientId
              LEFT JOIN dbo.Payer py ON py.PayerId = c.PayerId
              WHERE c.BranchId = @branchId ORDER BY c.ClaimId DESC", new { branchId }, cancellationToken: ct));
        return rows.ToList();
    }

    public async Task<IReadOnlyList<(string, int)>> GetStatusCountsAsync(int branchId, CancellationToken ct = default)
    {
        using var c = await _f.CreateOpenConnectionAsync(ct);
        var rows = await c.QueryAsync<(string, int)>(new CommandDefinition(
            "SELECT Status, COUNT(1) FROM dbo.Claim WHERE BranchId = @branchId GROUP BY Status", new { branchId }, cancellationToken: ct));
        return rows.ToList();
    }

    public async Task<long> InsertReconciliationAsync(SettlementReconciliation r, CancellationToken ct = default)
    {
        using var c = await _f.CreateOpenConnectionAsync(ct);
        const string sql = @"
INSERT INTO dbo.SettlementReconciliation (ClaimId, Utr, BankAmount, ReconciledUtc, Status)
VALUES (@ClaimId, @Utr, @BankAmount, @ReconciledUtc, @Status);
SELECT CAST(SCOPE_IDENTITY() AS BIGINT);";
        return await c.QuerySingleAsync<long>(new CommandDefinition(sql, r, cancellationToken: ct));
    }
}

public sealed class PmjayRepository : IPmjayRepository
{
    private readonly IDbConnectionFactory _f;
    public PmjayRepository(IDbConnectionFactory f) => _f = f;

    private sealed record PkgRow(int PackageId, decimal Rate);

    public async Task<(int PackageId, decimal Rate)?> GetPackageByCodeAsync(string code, CancellationToken ct = default)
    {
        using var c = await _f.CreateOpenConnectionAsync(ct);
        var row = await c.QuerySingleOrDefaultAsync<PkgRow>(new CommandDefinition(
            "SELECT PackageId, Rate FROM dbo.HbpPackage WHERE Code = @code AND IsActive = 1", new { code }, cancellationToken: ct));
        return row is null ? null : (row.PackageId, row.Rate);
    }

    public async Task<long> UpsertBeneficiaryAsync(PmjayBeneficiary b, CancellationToken ct = default)
    {
        using var c = await _f.CreateOpenConnectionAsync(ct);
        var existing = await c.QuerySingleOrDefaultAsync<long?>(new CommandDefinition(
            "SELECT BeneficiaryId FROM dbo.PmjayBeneficiary WHERE PatientId = @PatientId", b, cancellationToken: ct));
        if (existing is long id)
        {
            await c.ExecuteAsync(new CommandDefinition(
                @"UPDATE dbo.PmjayBeneficiary SET PmjayId = @PmjayId, BisVerified = @BisVerified, FamilyFloater = @FamilyFloater
                  WHERE BeneficiaryId = @id", new { b.PmjayId, b.BisVerified, b.FamilyFloater, id }, cancellationToken: ct));
            return id;
        }
        return await c.QuerySingleAsync<long>(new CommandDefinition(
            @"INSERT INTO dbo.PmjayBeneficiary (PatientId, PmjayId, BisVerified, FamilyFloater, UsedAmount)
              VALUES (@PatientId, @PmjayId, @BisVerified, @FamilyFloater, @UsedAmount);
              SELECT CAST(SCOPE_IDENTITY() AS BIGINT);", b, cancellationToken: ct));
    }

    public async Task<long> InsertCaseAsync(PmjayCase cse, CancellationToken ct = default)
    {
        using var c = await _f.CreateOpenConnectionAsync(ct);
        const string sql = @"
INSERT INTO dbo.PmjayCase (ClaimId, PackageId, TmsCaseNo, AyushmanMitra, AadhaarDischargeVerified)
VALUES (@ClaimId, @PackageId, @TmsCaseNo, @AyushmanMitra, @AadhaarDischargeVerified);
SELECT CAST(SCOPE_IDENTITY() AS BIGINT);";
        return await c.QuerySingleAsync<long>(new CommandDefinition(sql, cse, cancellationToken: ct));
    }

    public async Task<string> NextTmsNoAsync(int branchId, CancellationToken ct = default)
    {
        using var c = await _f.CreateOpenConnectionAsync(ct);
        return await c.QuerySingleAsync<string>(new CommandDefinition(
            "EXEC dbo.usp_NextDocNo @BranchId=@branchId, @DocType='TMS', @Prefix='TMS'", new { branchId }, cancellationToken: ct));
    }
}

public sealed class SchemeRepository : ISchemeRepository
{
    private readonly IDbConnectionFactory _f;
    public SchemeRepository(IDbConnectionFactory f) => _f = f;

    public async Task<long> UpsertMembershipAsync(SchemeMembership m, CancellationToken ct = default)
    {
        using var c = await _f.CreateOpenConnectionAsync(ct);
        var existing = await c.QuerySingleOrDefaultAsync<long?>(new CommandDefinition(
            "SELECT MembershipId FROM dbo.SchemeMembership WHERE PatientId = @PatientId AND SchemeType = @SchemeType", m, cancellationToken: ct));
        if (existing is long id)
        {
            await c.ExecuteAsync(new CommandDefinition(
                @"UPDATE dbo.SchemeMembership SET MemberNo = @MemberNo, SecondaryRef = @SecondaryRef, Verified = @Verified
                  WHERE MembershipId = @id", new { m.MemberNo, m.SecondaryRef, m.Verified, id }, cancellationToken: ct));
            return id;
        }
        return await c.QuerySingleAsync<long>(new CommandDefinition(
            @"INSERT INTO dbo.SchemeMembership (PatientId, SchemeType, MemberNo, SecondaryRef, Verified, ValidTo)
              VALUES (@PatientId, @SchemeType, @MemberNo, @SecondaryRef, @Verified, @ValidTo);
              SELECT CAST(SCOPE_IDENTITY() AS BIGINT);", m, cancellationToken: ct));
    }

    public async Task<IReadOnlyList<SchemePackage>> GetPackagesAsync(string schemeType, string? q, CancellationToken ct = default)
    {
        using var c = await _f.CreateOpenConnectionAsync(ct);
        var like = "%" + (q ?? "").Trim() + "%";
        return (await c.QueryAsync<SchemePackage>(new CommandDefinition(
            @"SELECT SchemePackageId, SchemeType, Code, Name, Rate, IsActive FROM dbo.SchemePackage
              WHERE SchemeType = @schemeType AND IsActive = 1 AND (@like = '%%' OR Code LIKE @like OR Name LIKE @like)
              ORDER BY Name", new { schemeType, like }, cancellationToken: ct))).ToList();
    }
}
