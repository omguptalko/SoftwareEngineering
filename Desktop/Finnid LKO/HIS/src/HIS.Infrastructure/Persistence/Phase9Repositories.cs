using Dapper;
using HIS.Application.Abstractions;
using HIS.Domain.Entities;

namespace HIS.Infrastructure.Persistence;

// L1.8.5 cutover: CompanyContract is master; exams/hazards/injuries/teleconsults are
// fiscal-scoped (FY DB). Patient/doctor/contract display names resolved app-side (D8).
public sealed class OccHealthRepository : IOccHealthRepository
{
    private readonly ITenantConnectionFactory _f;
    public OccHealthRepository(ITenantConnectionFactory f) => _f = f;

    public async Task<long> InsertContractAsync(CompanyContract c, CancellationToken ct = default)
    {
        using var conn = await _f.OpenMasterAsync(ct);
        const string sql = @"
INSERT INTO master.CompanyContract (CompanyName, PayerCode, ContractType, ValidFrom, ValidTo, IsActive)
VALUES (@CompanyName, @PayerCode, @ContractType, @ValidFrom, @ValidTo, @IsActive);
SELECT CAST(SCOPE_IDENTITY() AS BIGINT);";
        return await conn.QuerySingleAsync<long>(new CommandDefinition(sql, c, cancellationToken: ct));
    }

    public async Task<IReadOnlyList<CompanyContract>> GetContractsAsync(CancellationToken ct = default)
    {
        using var conn = await _f.OpenMasterAsync(ct);
        return (await conn.QueryAsync<CompanyContract>(new CommandDefinition(
            "SELECT * FROM master.CompanyContract WHERE IsActive = 1 ORDER BY CompanyName", cancellationToken: ct))).ToList();
    }

    public async Task<long> InsertExamAsync(MedicalExam e, CancellationToken ct = default)
    {
        using var conn = await _f.OpenDataAsync(ct);
        const string sql = @"
INSERT INTO occhealth.MedicalExam (BranchId, PatientId, ContractId, ExamType, ExamDate, FitnessResult, Audiometry, Spirometry, Vision, VaccinationNotes)
VALUES (@BranchId, @PatientId, @ContractId, @ExamType, @ExamDate, @FitnessResult, @Audiometry, @Spirometry, @Vision, @VaccinationNotes);
SELECT CAST(SCOPE_IDENTITY() AS BIGINT);";
        return await conn.QuerySingleAsync<long>(new CommandDefinition(sql, e, cancellationToken: ct));
    }

    public async Task<IReadOnlyList<(long, string, string?, string, string, string?)>> GetExamsAsync(int branchId, CancellationToken ct = default)
    {
        using var conn = await _f.OpenDataAsync(ct);
        var exams = (await conn.QueryAsync<(long ExamId, long? PatientId, int? ContractId, string ExamType, string ExamDate, string? FitnessResult)>(new CommandDefinition(
            @"SELECT ExamId, PatientId, ContractId, ExamType, CONVERT(varchar(10), ExamDate, 105) AS ExamDate, FitnessResult
              FROM occhealth.MedicalExam WHERE BranchId = @branchId ORDER BY ExamId DESC", new { branchId }, cancellationToken: ct))).ToList();
        var pats = await MasterLookup.PatientNamesAsync(_f, exams.Where(e => e.PatientId.HasValue).Select(e => e.PatientId!.Value), ct);
        var contractIds = exams.Where(e => e.ContractId.HasValue).Select(e => e.ContractId!.Value).Distinct().ToArray();
        var contracts = new Dictionary<int, string>();
        if (contractIds.Length > 0)
        {
            using var m = await _f.OpenMasterAsync(ct);
            contracts = (await m.QueryAsync<(int ContractId, string CompanyName)>(new CommandDefinition(
                "SELECT ContractId, CompanyName FROM master.CompanyContract WHERE ContractId IN @contractIds", new { contractIds }, cancellationToken: ct)))
                .ToDictionary(r => r.ContractId, r => r.CompanyName);
        }
        return exams.Select(e => (e.ExamId,
            e.PatientId.HasValue ? pats.GetValueOrDefault(e.PatientId.Value, "") : "",
            e.ContractId.HasValue ? contracts.GetValueOrDefault(e.ContractId.Value) : null,
            e.ExamType, e.ExamDate, e.FitnessResult)).ToList();
    }

    public async Task<long> InsertHazardAsync(HazardExposure h, CancellationToken ct = default)
    {
        using var conn = await _f.OpenDataAsync(ct);
        const string sql = @"
INSERT INTO occhealth.HazardExposure (PatientId, HazardType, RecordedDate, Notes)
VALUES (@PatientId, @HazardType, @RecordedDate, @Notes);
SELECT CAST(SCOPE_IDENTITY() AS BIGINT);";
        return await conn.QuerySingleAsync<long>(new CommandDefinition(sql, h, cancellationToken: ct));
    }

    public async Task<long> InsertInjuryAsync(WorkplaceInjury i, CancellationToken ct = default)
    {
        using var conn = await _f.OpenDataAsync(ct);
        const string sql = @"
INSERT INTO occhealth.WorkplaceInjury (PatientId, ContractId, InjuryDate, MlcLinked, Description)
VALUES (@PatientId, @ContractId, @InjuryDate, @MlcLinked, @Description);
SELECT CAST(SCOPE_IDENTITY() AS BIGINT);";
        return await conn.QuerySingleAsync<long>(new CommandDefinition(sql, i, cancellationToken: ct));
    }

    public async Task<IReadOnlyList<(long, string, string, bool, string?)>> GetInjuriesAsync(int branchId, CancellationToken ct = default)
    {
        using var conn = await _f.OpenDataAsync(ct);
        var inj = (await conn.QueryAsync<(long InjuryId, long PatientId, string InjuryDate, bool MlcLinked, string? Description)>(new CommandDefinition(
            @"SELECT InjuryId, PatientId, CONVERT(varchar(16), InjuryDate, 120) AS InjuryDate, MlcLinked, Description
              FROM occhealth.WorkplaceInjury ORDER BY InjuryId DESC", cancellationToken: ct))).ToList();
        var pats = await MasterLookup.PatientNamesAsync(_f, inj.Select(i => i.PatientId), ct);
        return inj.Select(i => (i.InjuryId, pats.GetValueOrDefault(i.PatientId, ""), i.InjuryDate, i.MlcLinked, i.Description)).ToList();
    }
}

public sealed class TelemedicineRepository : ITelemedicineRepository
{
    private readonly ITenantConnectionFactory _f;
    public TelemedicineRepository(ITenantConnectionFactory f) => _f = f;

    public async Task<int?> GetDoctorIdByCodeAsync(string code, CancellationToken ct = default)
    {
        using var c = await _f.OpenMasterAsync(ct);
        return await c.QuerySingleOrDefaultAsync<int?>(new CommandDefinition(
            "SELECT DoctorId FROM master.Doctor WHERE Code = @code AND IsActive = 1", new { code }, cancellationToken: ct));
    }

    public async Task<long> InsertTeleAsync(TeleConsult t, CancellationToken ct = default)
    {
        using var c = await _f.OpenDataAsync(ct);
        const string sql = @"
INSERT INTO telemedicine.TeleConsult (PatientId, DoctorId, FromBranchId, ToBranchId, ConsultType, ScheduledUtc, ConsentCaptured, EPrescriptionSigned, SessionAuditUrl, Status)
VALUES (@PatientId, @DoctorId, @FromBranchId, @ToBranchId, @ConsultType, @ScheduledUtc, @ConsentCaptured, @EPrescriptionSigned, @SessionAuditUrl, @Status);
SELECT CAST(SCOPE_IDENTITY() AS BIGINT);";
        return await c.QuerySingleAsync<long>(new CommandDefinition(sql, t, cancellationToken: ct));
    }

    public async Task<TeleConsult?> GetTeleAsync(long teleId, CancellationToken ct = default)
    {
        using var c = await _f.OpenDataAsync(ct);
        return await c.QuerySingleOrDefaultAsync<TeleConsult>(new CommandDefinition(
            "SELECT * FROM telemedicine.TeleConsult WHERE TeleId = @teleId", new { teleId }, cancellationToken: ct));
    }

    public async Task UpdateTeleAsync(TeleConsult t, CancellationToken ct = default)
    {
        using var c = await _f.OpenDataAsync(ct);
        await c.ExecuteAsync(new CommandDefinition(
            @"UPDATE telemedicine.TeleConsult SET ConsentCaptured = @ConsentCaptured, EPrescriptionSigned = @EPrescriptionSigned,
                     SessionAuditUrl = @SessionAuditUrl, Status = @Status WHERE TeleId = @TeleId", t, cancellationToken: ct));
    }

    public async Task<IReadOnlyList<(long, string, string?, string?, string?, bool, bool, string)>> GetTeleListAsync(int branchId, CancellationToken ct = default)
    {
        using var c = await _f.OpenDataAsync(ct);
        var tele = (await c.QueryAsync<(long TeleId, long PatientId, int? DoctorId, string? ConsultType, string? Scheduled, bool Consent, bool Signed, string Status)>(new CommandDefinition(
            @"SELECT TeleId, PatientId, DoctorId, ConsultType,
                     CONVERT(varchar(16), ScheduledUtc, 120) AS Scheduled, ConsentCaptured, EPrescriptionSigned, Status
              FROM telemedicine.TeleConsult WHERE FromBranchId = @branchId OR ToBranchId = @branchId
              ORDER BY TeleId DESC", new { branchId }, cancellationToken: ct))).ToList();
        var pats = await MasterLookup.PatientNamesAsync(_f, tele.Select(t => t.PatientId), ct);
        var docs = await MasterLookup.DoctorNamesAsync(_f, tele.Select(t => t.DoctorId), ct);
        return tele.Select(t => (t.TeleId, pats.GetValueOrDefault(t.PatientId, ""),
            t.DoctorId.HasValue ? docs.GetValueOrDefault(t.DoctorId.Value) : null,
            t.ConsultType, t.Scheduled, t.Consent, t.Signed, t.Status)).ToList();
    }
}
