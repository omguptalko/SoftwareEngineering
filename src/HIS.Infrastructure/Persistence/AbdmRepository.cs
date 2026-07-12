using Dapper;
using HIS.Application.Abstractions;
using HIS.Domain.Entities;

namespace HIS.Infrastructure.Persistence;

/// <summary>
/// ABDM / ABHA data access (SRS §6.2). Consent artifacts, HFR facilities and HPR
/// professionals are all longitudinal → tenant master DB. Display names (patient /
/// branch / doctor) are resolved app-side (D8 two-step); no cross-DB joins.
/// </summary>
public sealed class AbdmRepository : IAbdmRepository
{
    private readonly ITenantConnectionFactory _f;
    public AbdmRepository(ITenantConnectionFactory f) => _f = f;

    // ---- Consent artifacts (abdm.AbdmConsent) --------------------------------
    public async Task<long> InsertConsentAsync(AbdmConsent c, CancellationToken ct = default)
    {
        using var conn = await _f.OpenMasterAsync(ct);
        const string sql = @"
INSERT INTO abdm.AbdmConsent (PatientId, AbhaNumber, Purpose, HiTypes, GrantedUtc, ExpiryUtc, Status, FhirBundleUrl)
VALUES (@PatientId, @AbhaNumber, @Purpose, @HiTypes, @GrantedUtc, @ExpiryUtc, @Status, @FhirBundleUrl);
SELECT CAST(SCOPE_IDENTITY() AS BIGINT);";
        return await conn.QuerySingleAsync<long>(new CommandDefinition(sql, c, cancellationToken: ct));
    }

    public async Task<AbdmConsent?> GetConsentAsync(long consentArtifactId, CancellationToken ct = default)
    {
        using var conn = await _f.OpenMasterAsync(ct);
        return await conn.QuerySingleOrDefaultAsync<AbdmConsent>(new CommandDefinition(
            "SELECT * FROM abdm.AbdmConsent WHERE ConsentArtifactId = @consentArtifactId",
            new { consentArtifactId }, cancellationToken: ct));
    }

    public async Task UpdateConsentStatusAsync(long consentArtifactId, string status, DateTime? grantedUtc, DateTime? expiryUtc, CancellationToken ct = default)
    {
        using var conn = await _f.OpenMasterAsync(ct);
        await conn.ExecuteAsync(new CommandDefinition(
            @"UPDATE abdm.AbdmConsent
                 SET Status = @status,
                     GrantedUtc = COALESCE(@grantedUtc, GrantedUtc),
                     ExpiryUtc  = COALESCE(@expiryUtc, ExpiryUtc)
               WHERE ConsentArtifactId = @consentArtifactId",
            new { consentArtifactId, status, grantedUtc, expiryUtc }, cancellationToken: ct));
    }

    public async Task<IReadOnlyList<(long, string, string?, string?, string?, string, DateTime?, DateTime?)>> GetConsentsAsync(CancellationToken ct = default)
    {
        using var conn = await _f.OpenMasterAsync(ct);
        var rows = (await conn.QueryAsync<AbdmConsent>(new CommandDefinition(
            "SELECT * FROM abdm.AbdmConsent ORDER BY ConsentArtifactId DESC", cancellationToken: ct))).ToList();
        var names = await MasterLookup.PatientNamesAsync(_f, rows.Select(r => r.PatientId), ct);
        return rows.Select(r => (
            r.ConsentArtifactId,
            names.GetValueOrDefault(r.PatientId, "Patient #" + r.PatientId),
            r.AbhaNumber, r.Purpose, r.HiTypes, r.Status, r.GrantedUtc, r.ExpiryUtc)).ToList();
    }

    // ---- HFR facilities (master.HfrFacility) ---------------------------------
    public async Task<int> UpsertFacilityAsync(int branchId, string hfrCode, CancellationToken ct = default)
    {
        using var conn = await _f.OpenMasterAsync(ct);
        const string sql = @"
UPDATE master.HfrFacility SET HfrCode = @hfrCode, OnboardedUtc = SYSUTCDATETIME() WHERE BranchId = @branchId;
IF @@ROWCOUNT = 0
    INSERT INTO master.HfrFacility (BranchId, HfrCode, OnboardedUtc) VALUES (@branchId, @hfrCode, SYSUTCDATETIME());
SELECT HfrId FROM master.HfrFacility WHERE BranchId = @branchId;";
        return await conn.QuerySingleAsync<int>(new CommandDefinition(sql, new { branchId, hfrCode }, cancellationToken: ct));
    }

    public async Task<IReadOnlyList<(int, string, string?, DateTime?)>> GetFacilitiesAsync(CancellationToken ct = default)
    {
        using var conn = await _f.OpenMasterAsync(ct);
        var rows = (await conn.QueryAsync<(int HfrId, int BranchId, string? HfrCode, DateTime? OnboardedUtc)>(new CommandDefinition(
            "SELECT HfrId, BranchId, HfrCode, OnboardedUtc FROM master.HfrFacility ORDER BY HfrId DESC", cancellationToken: ct))).ToList();
        var ids = rows.Select(r => r.BranchId).Distinct().ToArray();
        var branches = ids.Length == 0 ? new Dictionary<int, string>()
            : (await conn.QueryAsync<(int BranchId, string Name)>(new CommandDefinition(
                "SELECT BranchId, Name FROM master.Branch WHERE BranchId IN @ids", new { ids }, cancellationToken: ct)))
                .ToDictionary(x => x.BranchId, x => x.Name);
        return rows.Select(r => (r.HfrId, branches.GetValueOrDefault(r.BranchId, "Branch #" + r.BranchId), r.HfrCode, r.OnboardedUtc)).ToList();
    }

    // ---- HPR professionals (master.HprProfessional) --------------------------
    public async Task<int?> GetDoctorIdByCodeAsync(string code, CancellationToken ct = default)
    {
        using var conn = await _f.OpenMasterAsync(ct);
        return await conn.QuerySingleOrDefaultAsync<int?>(new CommandDefinition(
            "SELECT DoctorId FROM master.Doctor WHERE Code = @code AND IsActive = 1", new { code }, cancellationToken: ct));
    }

    public async Task<int> UpsertProfessionalAsync(int doctorId, string hprCode, CancellationToken ct = default)
    {
        using var conn = await _f.OpenMasterAsync(ct);
        const string sql = @"
UPDATE master.HprProfessional SET HprCode = @hprCode, OnboardedUtc = SYSUTCDATETIME() WHERE DoctorId = @doctorId;
IF @@ROWCOUNT = 0
    INSERT INTO master.HprProfessional (DoctorId, HprCode, OnboardedUtc) VALUES (@doctorId, @hprCode, SYSUTCDATETIME());
SELECT HprId FROM master.HprProfessional WHERE DoctorId = @doctorId;";
        return await conn.QuerySingleAsync<int>(new CommandDefinition(sql, new { doctorId, hprCode }, cancellationToken: ct));
    }

    public async Task<IReadOnlyList<(int, string, string?, string?, DateTime?)>> GetProfessionalsAsync(CancellationToken ct = default)
    {
        using var conn = await _f.OpenMasterAsync(ct);
        var rows = (await conn.QueryAsync<(int HprId, int DoctorId, string? HprCode, DateTime? OnboardedUtc)>(new CommandDefinition(
            "SELECT HprId, DoctorId, HprCode, OnboardedUtc FROM master.HprProfessional ORDER BY HprId DESC", cancellationToken: ct))).ToList();
        var ids = rows.Select(r => r.DoctorId).Distinct().ToArray();
        var docs = ids.Length == 0 ? new Dictionary<int, (string Name, string? Department)>()
            : (await conn.QueryAsync<(int DoctorId, string Name, string? Department)>(new CommandDefinition(
                "SELECT DoctorId, Name, Department FROM master.Doctor WHERE DoctorId IN @ids", new { ids }, cancellationToken: ct)))
                .ToDictionary(x => x.DoctorId, x => (x.Name, x.Department));
        return rows.Select(r =>
        {
            var d = docs.GetValueOrDefault(r.DoctorId, ("Doctor #" + r.DoctorId, (string?)null));
            return (r.HprId, d.Item1, d.Item2, r.HprCode, r.OnboardedUtc);
        }).ToList();
    }
}
