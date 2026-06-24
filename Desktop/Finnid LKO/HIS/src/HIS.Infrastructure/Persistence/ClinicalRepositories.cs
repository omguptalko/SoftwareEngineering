using Dapper;
using HIS.Application.Abstractions;
using HIS.Domain.Entities;

namespace HIS.Infrastructure.Persistence;

public sealed class AppointmentRepository : IAppointmentRepository
{
    private readonly IDbConnectionFactory _f;
    public AppointmentRepository(IDbConnectionFactory f) => _f = f;

    public async Task<int?> GetDoctorIdByCodeAsync(string code, CancellationToken ct = default)
    {
        using var c = await _f.CreateOpenConnectionAsync(ct);
        return await c.QuerySingleOrDefaultAsync<int?>(new CommandDefinition(
            "SELECT DoctorId FROM dbo.Doctor WHERE Code = @code AND IsActive = 1", new { code }, cancellationToken: ct));
    }

    public async Task<IReadOnlyList<DateTime>> GetBookedSlotStartsAsync(int doctorId, DateTime date, CancellationToken ct = default)
    {
        using var c = await _f.CreateOpenConnectionAsync(ct);
        return (await c.QueryAsync<DateTime>(new CommandDefinition(
            @"SELECT SlotStart FROM dbo.Appointment
              WHERE DoctorId = @doctorId AND CAST(SlotStart AS DATE) = @date AND Status <> 'Cancelled'",
            new { doctorId, date = date.Date }, cancellationToken: ct))).ToList();
    }

    public async Task<string> NextTokenAsync(int branchId, int doctorId, DateTime date, CancellationToken ct = default)
    {
        using var c = await _f.CreateOpenConnectionAsync(ct);
        var count = await c.ExecuteScalarAsync<int>(new CommandDefinition(
            @"SELECT COUNT(1) FROM dbo.Appointment
              WHERE BranchId = @branchId AND DoctorId = @doctorId AND CAST(SlotStart AS DATE) = @date",
            new { branchId, doctorId, date = date.Date }, cancellationToken: ct));
        return $"T-{count + 1:D2}";
    }

    public async Task<long> InsertAsync(Appointment a, CancellationToken ct = default)
    {
        using var c = await _f.CreateOpenConnectionAsync(ct);
        const string sql = @"
INSERT INTO dbo.Appointment (BranchId, PatientId, DoctorId, Department, SlotStart, VisitType, Mode, TokenNo, Status, CreatedUtc)
VALUES (@BranchId, @PatientId, @DoctorId, @Department, @SlotStart, @VisitType, @Mode, @TokenNo, @Status, @CreatedUtc);
SELECT CAST(SCOPE_IDENTITY() AS BIGINT);";
        return await c.QuerySingleAsync<long>(new CommandDefinition(sql, a, cancellationToken: ct));
    }

    public async Task<IReadOnlyList<(string, string, string, string)>> GetTodayQueueAsync(int branchId, int? doctorId, DateTime date, CancellationToken ct = default)
    {
        using var c = await _f.CreateOpenConnectionAsync(ct);
        var rows = await c.QueryAsync<(string, string, string, string)>(new CommandDefinition(
            @"SELECT ISNULL(a.TokenNo,'') AS TokenNo,
                     ISNULL(p.FullName,'(walk-in)') AS PatientName,
                     ISNULL(d.Name,'') AS DoctorName,
                     a.Status
              FROM dbo.Appointment a
              LEFT JOIN dbo.Patient p ON p.PatientId = a.PatientId
              LEFT JOIN dbo.Doctor  d ON d.DoctorId  = a.DoctorId
              WHERE a.BranchId = @branchId AND CAST(a.SlotStart AS DATE) = @date
                    AND (@doctorId IS NULL OR a.DoctorId = @doctorId)
              ORDER BY a.SlotStart, a.AppointmentId",
            new { branchId, date = date.Date, doctorId }, cancellationToken: ct));
        return rows.ToList();
    }
}

public sealed class EncounterRepository : IEncounterRepository
{
    private readonly IDbConnectionFactory _f;
    public EncounterRepository(IDbConnectionFactory f) => _f = f;

    public async Task<int?> GetDoctorIdByCodeAsync(string code, CancellationToken ct = default)
    {
        using var c = await _f.CreateOpenConnectionAsync(ct);
        return await c.QuerySingleOrDefaultAsync<int?>(new CommandDefinition(
            "SELECT DoctorId FROM dbo.Doctor WHERE Code = @code AND IsActive = 1", new { code }, cancellationToken: ct));
    }

    public async Task<int?> GetDrugIdByCodeAsync(string code, CancellationToken ct = default)
    {
        using var c = await _f.CreateOpenConnectionAsync(ct);
        return await c.QuerySingleOrDefaultAsync<int?>(new CommandDefinition(
            "SELECT DrugId FROM dbo.Drug WHERE Code = @code AND IsActive = 1", new { code }, cancellationToken: ct));
    }

    public async Task<long> CreateEncounterAsync(Encounter e, CancellationToken ct = default)
    {
        using var c = await _f.CreateOpenConnectionAsync(ct);
        const string sql = @"
INSERT INTO dbo.Encounter (BranchId, PatientId, DoctorId, EncType, StartedUtc, Complaints, History, Advice, FollowUpDate, Status)
VALUES (@BranchId, @PatientId, @DoctorId, @EncType, @StartedUtc, @Complaints, @History, @Advice, @FollowUpDate, @Status);
SELECT CAST(SCOPE_IDENTITY() AS BIGINT);";
        return await c.QuerySingleAsync<long>(new CommandDefinition(sql, e, cancellationToken: ct));
    }

    public async Task SaveVitalsAsync(Vitals v, CancellationToken ct = default)
    {
        using var c = await _f.CreateOpenConnectionAsync(ct);
        const string sql = @"
INSERT INTO dbo.Vitals (EncounterId, RecordedUtc, TempF, Pulse, BpSystolic, BpDiastolic, Spo2, RespRate, WeightKg, HeightCm, Grbs)
VALUES (@EncounterId, @RecordedUtc, @TempF, @Pulse, @BpSystolic, @BpDiastolic, @Spo2, @RespRate, @WeightKg, @HeightCm, @Grbs);";
        await c.ExecuteAsync(new CommandDefinition(sql, v, cancellationToken: ct));
    }

    public async Task AddDiagnosisAsync(long encounterId, string icd10, bool provisional, CancellationToken ct = default)
    {
        using var c = await _f.CreateOpenConnectionAsync(ct);
        // Only link valid ICD-10 codes (FK-safe); silently ignore unknown free-text.
        await c.ExecuteAsync(new CommandDefinition(
            @"IF EXISTS (SELECT 1 FROM dbo.Icd10Code WHERE Code = @icd10)
              INSERT INTO dbo.EncounterDiagnosis (EncounterId, Icd10Code, IsProvisional)
              VALUES (@encounterId, @icd10, @provisional);",
            new { encounterId, icd10, provisional }, cancellationToken: ct));
    }

    public async Task<long> CreatePrescriptionAsync(long encounterId, CancellationToken ct = default)
    {
        using var c = await _f.CreateOpenConnectionAsync(ct);
        const string sql = @"
INSERT INTO dbo.Prescription (EncounterId, CreatedUtc, Status) VALUES (@encounterId, SYSUTCDATETIME(), 'Pending');
SELECT CAST(SCOPE_IDENTITY() AS BIGINT);";
        return await c.QuerySingleAsync<long>(new CommandDefinition(sql, new { encounterId }, cancellationToken: ct));
    }

    public async Task AddPrescriptionLineAsync(PrescriptionLine l, CancellationToken ct = default)
    {
        using var c = await _f.CreateOpenConnectionAsync(ct);
        const string sql = @"
INSERT INTO dbo.PrescriptionLine (PrescriptionId, DrugId, Dose, Frequency, Days, Route, Qty)
VALUES (@PrescriptionId, @DrugId, @Dose, @Frequency, @Days, @Route, @Qty);";
        await c.ExecuteAsync(new CommandDefinition(sql, l, cancellationToken: ct));
    }
}
