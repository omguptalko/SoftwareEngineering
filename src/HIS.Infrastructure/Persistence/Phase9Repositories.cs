using Dapper;
using HIS.Application.Abstractions;
using HIS.Domain.Entities;

namespace HIS.Infrastructure.Persistence;

public sealed class OccHealthRepository : IOccHealthRepository
{
    private readonly IDbConnectionFactory _f;
    public OccHealthRepository(IDbConnectionFactory f) => _f = f;

    public async Task<long> InsertContractAsync(CompanyContract c, CancellationToken ct = default)
    {
        using var conn = await _f.CreateOpenConnectionAsync(ct);
        const string sql = @"
INSERT INTO dbo.CompanyContract (CompanyName, PayerCode, ContractType, ValidFrom, ValidTo, IsActive)
VALUES (@CompanyName, @PayerCode, @ContractType, @ValidFrom, @ValidTo, @IsActive);
SELECT CAST(SCOPE_IDENTITY() AS BIGINT);";
        return await conn.QuerySingleAsync<long>(new CommandDefinition(sql, c, cancellationToken: ct));
    }

    public async Task<IReadOnlyList<CompanyContract>> GetContractsAsync(CancellationToken ct = default)
    {
        using var conn = await _f.CreateOpenConnectionAsync(ct);
        return (await conn.QueryAsync<CompanyContract>(new CommandDefinition(
            "SELECT * FROM dbo.CompanyContract WHERE IsActive = 1 ORDER BY CompanyName", cancellationToken: ct))).ToList();
    }

    public async Task<long> InsertExamAsync(MedicalExam e, CancellationToken ct = default)
    {
        using var conn = await _f.CreateOpenConnectionAsync(ct);
        const string sql = @"
INSERT INTO dbo.MedicalExam (BranchId, PatientId, ContractId, ExamType, ExamDate, FitnessResult, Audiometry, Spirometry, Vision, VaccinationNotes)
VALUES (@BranchId, @PatientId, @ContractId, @ExamType, @ExamDate, @FitnessResult, @Audiometry, @Spirometry, @Vision, @VaccinationNotes);
SELECT CAST(SCOPE_IDENTITY() AS BIGINT);";
        return await conn.QuerySingleAsync<long>(new CommandDefinition(sql, e, cancellationToken: ct));
    }

    public async Task<IReadOnlyList<(long, string, string?, string, string, string?)>> GetExamsAsync(int branchId, CancellationToken ct = default)
    {
        using var conn = await _f.CreateOpenConnectionAsync(ct);
        var rows = await conn.QueryAsync<(long, string, string?, string, string, string?)>(new CommandDefinition(
            @"SELECT e.ExamId, ISNULL(p.FullName,'') AS Patient, cc.CompanyName,
                     e.ExamType, CONVERT(varchar(10), e.ExamDate, 105) AS ExamDate, e.FitnessResult
              FROM dbo.MedicalExam e
              LEFT JOIN dbo.Patient p ON p.PatientId = e.PatientId
              LEFT JOIN dbo.CompanyContract cc ON cc.ContractId = e.ContractId
              WHERE e.BranchId = @branchId ORDER BY e.ExamId DESC", new { branchId }, cancellationToken: ct));
        return rows.ToList();
    }

    public async Task<long> InsertHazardAsync(HazardExposure h, CancellationToken ct = default)
    {
        using var conn = await _f.CreateOpenConnectionAsync(ct);
        const string sql = @"
INSERT INTO dbo.HazardExposure (PatientId, HazardType, RecordedDate, Notes)
VALUES (@PatientId, @HazardType, @RecordedDate, @Notes);
SELECT CAST(SCOPE_IDENTITY() AS BIGINT);";
        return await conn.QuerySingleAsync<long>(new CommandDefinition(sql, h, cancellationToken: ct));
    }

    public async Task<long> InsertInjuryAsync(WorkplaceInjury i, CancellationToken ct = default)
    {
        using var conn = await _f.CreateOpenConnectionAsync(ct);
        const string sql = @"
INSERT INTO dbo.WorkplaceInjury (PatientId, ContractId, InjuryDate, MlcLinked, Description)
VALUES (@PatientId, @ContractId, @InjuryDate, @MlcLinked, @Description);
SELECT CAST(SCOPE_IDENTITY() AS BIGINT);";
        return await conn.QuerySingleAsync<long>(new CommandDefinition(sql, i, cancellationToken: ct));
    }

    public async Task<IReadOnlyList<(long, string, string, bool, string?)>> GetInjuriesAsync(int branchId, CancellationToken ct = default)
    {
        using var conn = await _f.CreateOpenConnectionAsync(ct);
        var rows = await conn.QueryAsync<(long, string, string, bool, string?)>(new CommandDefinition(
            @"SELECT i.InjuryId, ISNULL(p.FullName,'') AS Patient,
                     CONVERT(varchar(16), i.InjuryDate, 120) AS InjuryDate, i.MlcLinked, i.Description
              FROM dbo.WorkplaceInjury i INNER JOIN dbo.Patient p ON p.PatientId = i.PatientId
              WHERE p.RegBranchId = @branchId ORDER BY i.InjuryId DESC", new { branchId }, cancellationToken: ct));
        return rows.ToList();
    }
}

public sealed class TelemedicineRepository : ITelemedicineRepository
{
    private readonly IDbConnectionFactory _f;
    public TelemedicineRepository(IDbConnectionFactory f) => _f = f;

    public async Task<int?> GetDoctorIdByCodeAsync(string code, CancellationToken ct = default)
    {
        using var c = await _f.CreateOpenConnectionAsync(ct);
        return await c.QuerySingleOrDefaultAsync<int?>(new CommandDefinition(
            "SELECT DoctorId FROM dbo.Doctor WHERE Code = @code AND IsActive = 1", new { code }, cancellationToken: ct));
    }

    public async Task<long> InsertTeleAsync(TeleConsult t, CancellationToken ct = default)
    {
        using var c = await _f.CreateOpenConnectionAsync(ct);
        const string sql = @"
INSERT INTO dbo.TeleConsult (PatientId, DoctorId, FromBranchId, ToBranchId, ConsultType, ScheduledUtc, ConsentCaptured, EPrescriptionSigned, SessionAuditUrl, Status)
VALUES (@PatientId, @DoctorId, @FromBranchId, @ToBranchId, @ConsultType, @ScheduledUtc, @ConsentCaptured, @EPrescriptionSigned, @SessionAuditUrl, @Status);
SELECT CAST(SCOPE_IDENTITY() AS BIGINT);";
        return await c.QuerySingleAsync<long>(new CommandDefinition(sql, t, cancellationToken: ct));
    }

    public async Task<TeleConsult?> GetTeleAsync(long teleId, CancellationToken ct = default)
    {
        using var c = await _f.CreateOpenConnectionAsync(ct);
        return await c.QuerySingleOrDefaultAsync<TeleConsult>(new CommandDefinition(
            "SELECT * FROM dbo.TeleConsult WHERE TeleId = @teleId", new { teleId }, cancellationToken: ct));
    }

    public async Task UpdateTeleAsync(TeleConsult t, CancellationToken ct = default)
    {
        using var c = await _f.CreateOpenConnectionAsync(ct);
        await c.ExecuteAsync(new CommandDefinition(
            @"UPDATE dbo.TeleConsult SET ConsentCaptured = @ConsentCaptured, EPrescriptionSigned = @EPrescriptionSigned,
                     SessionAuditUrl = @SessionAuditUrl, Status = @Status WHERE TeleId = @TeleId", t, cancellationToken: ct));
    }

    public async Task<IReadOnlyList<(long, string, string?, string?, string?, bool, bool, string)>> GetTeleListAsync(int branchId, CancellationToken ct = default)
    {
        using var c = await _f.CreateOpenConnectionAsync(ct);
        var rows = await c.QueryAsync<(long, string, string?, string?, string?, bool, bool, string)>(new CommandDefinition(
            @"SELECT t.TeleId, ISNULL(p.FullName,'') AS Patient, d.Name AS Doctor, t.ConsultType,
                     CONVERT(varchar(16), t.ScheduledUtc, 120) AS Scheduled, t.ConsentCaptured, t.EPrescriptionSigned, t.Status
              FROM dbo.TeleConsult t
              LEFT JOIN dbo.Patient p ON p.PatientId = t.PatientId
              LEFT JOIN dbo.Doctor d ON d.DoctorId = t.DoctorId
              WHERE t.FromBranchId = @branchId OR t.ToBranchId = @branchId
              ORDER BY t.TeleId DESC", new { branchId }, cancellationToken: ct));
        return rows.ToList();
    }
}
