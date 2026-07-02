using Dapper;
using HIS.Application.Abstractions;
using HIS.Domain.Entities;

namespace HIS.Infrastructure.Persistence;

public sealed class AppointmentRepository : IAppointmentRepository
{
    private readonly ITenantConnectionFactory _f;
    public AppointmentRepository(ITenantConnectionFactory f) => _f = f;

    public async Task<int?> GetDoctorIdByCodeAsync(string code, CancellationToken ct = default)
    {
        using var c = await _f.OpenMasterAsync(ct);
        return await c.QuerySingleOrDefaultAsync<int?>(new CommandDefinition(
            "SELECT DoctorId FROM master.Doctor WHERE Code = @code AND IsActive = 1", new { code }, cancellationToken: ct));
    }

    public async Task<IReadOnlyList<DateTime>> GetBookedSlotStartsAsync(int doctorId, DateTime date, CancellationToken ct = default)
    {
        using var c = await _f.OpenMasterAsync(ct);
        return (await c.QueryAsync<DateTime>(new CommandDefinition(
            @"SELECT SlotStart FROM clinical.Appointment
              WHERE DoctorId = @doctorId AND CAST(SlotStart AS DATE) = @date AND Status <> 'Cancelled'",
            new { doctorId, date = date.Date }, cancellationToken: ct))).ToList();
    }

    public async Task<string> NextTokenAsync(int branchId, int doctorId, DateTime date, CancellationToken ct = default)
    {
        using var c = await _f.OpenMasterAsync(ct);
        var count = await c.ExecuteScalarAsync<int>(new CommandDefinition(
            @"SELECT COUNT(1) FROM clinical.Appointment
              WHERE BranchId = @branchId AND DoctorId = @doctorId AND CAST(SlotStart AS DATE) = @date",
            new { branchId, doctorId, date = date.Date }, cancellationToken: ct));
        return $"T-{count + 1:D2}";
    }

    public async Task<long> InsertAsync(Appointment a, CancellationToken ct = default)
    {
        using var c = await _f.OpenMasterAsync(ct);
        const string sql = @"
INSERT INTO clinical.Appointment (BranchId, PatientId, DoctorId, Department, SlotStart, VisitType, Mode, TokenNo, Status, CreatedUtc)
VALUES (@BranchId, @PatientId, @DoctorId, @Department, @SlotStart, @VisitType, @Mode, @TokenNo, @Status, @CreatedUtc);
SELECT CAST(SCOPE_IDENTITY() AS BIGINT);";
        return await c.QuerySingleAsync<long>(new CommandDefinition(sql, a, cancellationToken: ct));
    }

    public async Task<IReadOnlyList<(long, string, string, string, string, string, bool, DateTime)>> GetTodayQueueAsync(int branchId, int? doctorId, DateTime date, string? status, CancellationToken ct = default)
    {
        using var c = await _f.OpenMasterAsync(ct);
        var rows = await c.QueryAsync<(long, string, string, string, string, string, bool, DateTime)>(new CommandDefinition(
            @"SELECT a.AppointmentId,
                     ISNULL(a.TokenNo,'') AS TokenNo,
                     ISNULL(p.Uhid,'') AS Uhid,
                     ISNULL(p.FullName,'(walk-in)') AS PatientName,
                     ISNULL(d.Name,'') AS DoctorName,
                     a.Status,
                     CAST(CASE WHEN EXISTS (SELECT 1 FROM clinical.Vitals v WHERE v.AppointmentId = a.AppointmentId) THEN 1 ELSE 0 END AS BIT) AS HasVitals,
                     a.SlotStart
              FROM clinical.Appointment a
              LEFT JOIN patient.Patient p ON p.PatientId = a.PatientId
              LEFT JOIN master.Doctor  d ON d.DoctorId  = a.DoctorId
              WHERE a.BranchId = @branchId AND CAST(a.SlotStart AS DATE) = @date
                    AND (@doctorId IS NULL OR a.DoctorId = @doctorId)
                    AND (@status IS NULL OR a.Status = @status)
              ORDER BY a.SlotStart, a.AppointmentId",
            new { branchId, date = date.Date, doctorId, status }, cancellationToken: ct));
        return rows.ToList();
    }

    public async Task<IReadOnlyList<(long, DateTime, string, string, string, string, string, string, string)>> GetUpcomingAsync(int branchId, DateTime fromDate, int? doctorId, bool followUpOnly, CancellationToken ct = default)
    {
        using var c = await _f.OpenMasterAsync(ct);
        var rows = await c.QueryAsync<(long, DateTime, string, string, string, string, string, string, string)>(new CommandDefinition(
            @"SELECT a.AppointmentId,
                     a.SlotStart,
                     ISNULL(a.TokenNo,'') AS TokenNo,
                     ISNULL(p.Uhid,'') AS Uhid,
                     ISNULL(p.FullName,'(walk-in)') AS PatientName,
                     ISNULL(d.Name,'') AS DoctorName,
                     ISNULL(a.Department,'') AS Department,
                     ISNULL(a.VisitType,'') AS VisitType,
                     a.Status
              FROM clinical.Appointment a
              LEFT JOIN patient.Patient p ON p.PatientId = a.PatientId
              LEFT JOIN master.Doctor  d ON d.DoctorId  = a.DoctorId
              WHERE a.BranchId = @branchId
                    AND CAST(a.SlotStart AS DATE) >= @fromDate
                    AND a.Status NOT IN ('Cancelled', 'Completed')
                    AND (@doctorId IS NULL OR a.DoctorId = @doctorId)
                    AND (@followUpOnly = 0 OR a.VisitType = 'Follow-up')
              ORDER BY a.SlotStart, a.AppointmentId",
            new { branchId, fromDate = fromDate.Date, doctorId, followUpOnly = followUpOnly ? 1 : 0 }, cancellationToken: ct));
        return rows.ToList();
    }

    public async Task<(long? PatientId, int DoctorId, int BranchId, string Status)?> GetAppointmentAsync(long appointmentId, CancellationToken ct = default)
    {
        using var c = await _f.OpenMasterAsync(ct);
        var list = (await c.QueryAsync<(long? PatientId, int DoctorId, int BranchId, string Status)>(new CommandDefinition(
            "SELECT PatientId, DoctorId, BranchId, Status FROM clinical.Appointment WHERE AppointmentId = @appointmentId",
            new { appointmentId }, cancellationToken: ct))).ToList();
        return list.Count == 0 ? null : list[0];
    }

    public async Task SetStatusAsync(long appointmentId, string status, CancellationToken ct = default)
    {
        using var c = await _f.OpenMasterAsync(ct);
        await c.ExecuteAsync(new CommandDefinition(
            "UPDATE clinical.Appointment SET Status = @status WHERE AppointmentId = @appointmentId",
            new { appointmentId, status }, cancellationToken: ct));
    }

    public async Task MarkCalledAsync(long appointmentId, CancellationToken ct = default)
    {
        using var c = await _f.OpenMasterAsync(ct);
        await c.ExecuteAsync(new CommandDefinition(
            "UPDATE clinical.Appointment SET Status = 'InConsultation', CalledUtc = SYSUTCDATETIME() WHERE AppointmentId = @appointmentId",
            new { appointmentId }, cancellationToken: ct));
    }
}

public sealed class EncounterRepository : IEncounterRepository
{
    private readonly ITenantConnectionFactory _f;
    public EncounterRepository(ITenantConnectionFactory f) => _f = f;

    public async Task<int?> GetDoctorIdByCodeAsync(string code, CancellationToken ct = default)
    {
        using var c = await _f.OpenMasterAsync(ct);
        return await c.QuerySingleOrDefaultAsync<int?>(new CommandDefinition(
            "SELECT DoctorId FROM master.Doctor WHERE Code = @code AND IsActive = 1", new { code }, cancellationToken: ct));
    }

    public async Task<int?> GetDrugIdByCodeAsync(string code, CancellationToken ct = default)
    {
        using var c = await _f.OpenMasterAsync(ct);
        return await c.QuerySingleOrDefaultAsync<int?>(new CommandDefinition(
            "SELECT DrugId FROM master.Drug WHERE Code = @code AND IsActive = 1", new { code }, cancellationToken: ct));
    }

    public async Task<long> CreateEncounterAsync(Encounter e, CancellationToken ct = default)
    {
        using var c = await _f.OpenMasterAsync(ct);
        const string sql = @"
INSERT INTO clinical.Encounter (BranchId, PatientId, DoctorId, EncType, StartedUtc, Complaints, History, Advice, FollowUpDate, Status)
VALUES (@BranchId, @PatientId, @DoctorId, @EncType, @StartedUtc, @Complaints, @History, @Advice, @FollowUpDate, @Status);
SELECT CAST(SCOPE_IDENTITY() AS BIGINT);";
        return await c.QuerySingleAsync<long>(new CommandDefinition(sql, e, cancellationToken: ct));
    }

    public async Task SaveVitalsAsync(Vitals v, CancellationToken ct = default)
    {
        using var c = await _f.OpenMasterAsync(ct);
        const string sql = @"
INSERT INTO clinical.Vitals (EncounterId, RecordedUtc, TempF, Pulse, BpSystolic, BpDiastolic, Spo2, RespRate, WeightKg, HeightCm, Grbs)
VALUES (@EncounterId, @RecordedUtc, @TempF, @Pulse, @BpSystolic, @BpDiastolic, @Spo2, @RespRate, @WeightKg, @HeightCm, @Grbs);";
        await c.ExecuteAsync(new CommandDefinition(sql, v, cancellationToken: ct));
    }

    public async Task SaveApptVitalsAsync(long appointmentId, Vitals v, CancellationToken ct = default)
    {
        using var c = await _f.OpenMasterAsync(ct);
        const string sql = @"
INSERT INTO clinical.Vitals (AppointmentId, EncounterId, RecordedUtc, TempF, Pulse, BpSystolic, BpDiastolic, Spo2, RespRate, WeightKg, HeightCm, Grbs)
VALUES (@appointmentId, NULL, @RecordedUtc, @TempF, @Pulse, @BpSystolic, @BpDiastolic, @Spo2, @RespRate, @WeightKg, @HeightCm, @Grbs);";
        await c.ExecuteAsync(new CommandDefinition(sql,
            new { appointmentId, v.RecordedUtc, v.TempF, v.Pulse, v.BpSystolic, v.BpDiastolic, v.Spo2, v.RespRate, v.WeightKg, v.HeightCm, v.Grbs },
            cancellationToken: ct));
    }

    public async Task<Vitals?> GetApptVitalsAsync(long appointmentId, CancellationToken ct = default)
    {
        using var c = await _f.OpenMasterAsync(ct);
        return await c.QuerySingleOrDefaultAsync<Vitals?>(new CommandDefinition(
            @"SELECT TOP 1 VitalsId, EncounterId, AppointmentId, RecordedUtc, TempF, Pulse, BpSystolic, BpDiastolic, Spo2, RespRate, WeightKg, HeightCm, Grbs
              FROM clinical.Vitals WHERE AppointmentId = @appointmentId ORDER BY VitalsId DESC",
            new { appointmentId }, cancellationToken: ct));
    }

    public async Task LinkApptVitalsAsync(long appointmentId, long encounterId, CancellationToken ct = default)
    {
        using var c = await _f.OpenMasterAsync(ct);
        await c.ExecuteAsync(new CommandDefinition(
            "UPDATE clinical.Vitals SET EncounterId = @encounterId WHERE AppointmentId = @appointmentId AND EncounterId IS NULL",
            new { appointmentId, encounterId }, cancellationToken: ct));
    }

    public async Task<IReadOnlyList<(string Department, string Label, string FieldType, string? Options, int SortOrder)>> ListDeptTemplatesAsync(CancellationToken ct = default)
    {
        using var c = await _f.OpenMasterAsync(ct);
        return (await c.QueryAsync<(string Department, string Label, string FieldType, string? Options, int SortOrder)>(new CommandDefinition(
            "SELECT Department, Label, FieldType, Options, SortOrder FROM master.DeptTemplateField ORDER BY Department, SortOrder, Id",
            cancellationToken: ct))).ToList();
    }

    public async Task ReplaceDeptTemplateAsync(string department, IReadOnlyList<(string Label, string FieldType, string? Options)> fields, CancellationToken ct = default)
    {
        var allowed = new[] { "text", "number", "checkbox", "select" };
        using var c = await _f.OpenMasterAsync(ct);
        using var tx = c.BeginTransaction();
        await c.ExecuteAsync(new CommandDefinition(
            "DELETE FROM master.DeptTemplateField WHERE Department = @department",
            new { department }, transaction: tx, cancellationToken: ct));
        var clean = fields.Where(f => !string.IsNullOrWhiteSpace(f.Label)).ToList();
        for (var i = 0; i < clean.Count; i++)
        {
            var f = clean[i];
            var type = allowed.Contains((f.FieldType ?? "text").ToLowerInvariant()) ? f.FieldType!.ToLowerInvariant() : "text";
            await c.ExecuteAsync(new CommandDefinition(
                "INSERT master.DeptTemplateField (Department, Label, FieldType, Options, SortOrder) VALUES (@department, @label, @type, @options, @sort)",
                new { department, label = f.Label.Trim(), type, options = string.IsNullOrWhiteSpace(f.Options) ? null : f.Options.Trim(), sort = i + 1 },
                transaction: tx, cancellationToken: ct));
        }
        tx.Commit();
    }

    public async Task SaveTemplateAnswersAsync(long encounterId, string? department, IReadOnlyList<(string Label, string? FieldType, string? Value)> answers, CancellationToken ct = default)
    {
        var rows = answers.Where(a => !string.IsNullOrWhiteSpace(a.Value)).ToList();
        if (rows.Count == 0) return;
        using var c = await _f.OpenMasterAsync(ct);
        await c.ExecuteAsync(new CommandDefinition(
            @"INSERT INTO clinical.EncounterTemplateData (EncounterId, Department, FieldLabel, FieldType, Value)
              VALUES (@encounterId, @department, @label, @fieldType, @value);",
            rows.Select(a => new { encounterId, department, label = a.Label, fieldType = a.FieldType, value = a.Value }),
            cancellationToken: ct));
    }

    public async Task<IReadOnlyList<(string Label, string? FieldType, string? Value)>> GetTemplateAnswersAsync(long encounterId, CancellationToken ct = default)
    {
        using var c = await _f.OpenMasterAsync(ct);
        return (await c.QueryAsync<(string Label, string? FieldType, string? Value)>(new CommandDefinition(
            "SELECT FieldLabel, FieldType, Value FROM clinical.EncounterTemplateData WHERE EncounterId = @encounterId ORDER BY Id",
            new { encounterId }, cancellationToken: ct))).ToList();
    }

    public async Task<IReadOnlyList<(long EncounterId, DateTime StartedUtc, string? Doctor, string? Department, string? Diagnosis, string? Complaints)>> GetPatientEncountersAsync(long patientId, CancellationToken ct = default)
    {
        using var c = await _f.OpenMasterAsync(ct);
        return (await c.QueryAsync<(long EncounterId, DateTime StartedUtc, string? Doctor, string? Department, string? Diagnosis, string? Complaints)>(new CommandDefinition(
            @"SELECT e.EncounterId, e.StartedUtc, d.Name AS Doctor, d.Department,
                     (SELECT STRING_AGG(ed.Icd10Code, ', ') FROM clinical.EncounterDiagnosis ed WHERE ed.EncounterId = e.EncounterId) AS Diagnosis,
                     e.Complaints
              FROM clinical.Encounter e
              LEFT JOIN master.Doctor d ON d.DoctorId = e.DoctorId
              WHERE e.PatientId = @patientId
              ORDER BY e.StartedUtc DESC, e.EncounterId DESC",
            new { patientId }, cancellationToken: ct))).ToList();
    }

    public async Task AddDiagnosisAsync(long encounterId, string icd10, bool provisional, CancellationToken ct = default)
    {
        using var c = await _f.OpenMasterAsync(ct);
        // Only link valid ICD-10 codes (FK-safe); silently ignore unknown free-text.
        await c.ExecuteAsync(new CommandDefinition(
            @"IF EXISTS (SELECT 1 FROM master.Icd10Code WHERE Code = @icd10)
              INSERT INTO clinical.EncounterDiagnosis (EncounterId, Icd10Code, IsProvisional)
              VALUES (@encounterId, @icd10, @provisional);",
            new { encounterId, icd10, provisional }, cancellationToken: ct));
    }

    public async Task<long> CreatePrescriptionAsync(long encounterId, CancellationToken ct = default)
    {
        using var c = await _f.OpenMasterAsync(ct);
        const string sql = @"
INSERT INTO clinical.Prescription (EncounterId, CreatedUtc, Status) VALUES (@encounterId, SYSUTCDATETIME(), 'Pending');
SELECT CAST(SCOPE_IDENTITY() AS BIGINT);";
        return await c.QuerySingleAsync<long>(new CommandDefinition(sql, new { encounterId }, cancellationToken: ct));
    }

    public async Task AddPrescriptionLineAsync(PrescriptionLine l, CancellationToken ct = default)
    {
        using var c = await _f.OpenMasterAsync(ct);
        const string sql = @"
INSERT INTO clinical.PrescriptionLine (PrescriptionId, DrugId, Dose, Frequency, Days, Route, Qty)
VALUES (@PrescriptionId, @DrugId, @Dose, @Frequency, @Days, @Route, @Qty);";
        await c.ExecuteAsync(new CommandDefinition(sql, l, cancellationToken: ct));
    }
}
