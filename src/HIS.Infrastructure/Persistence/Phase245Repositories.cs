using Dapper;
using HIS.Application.Abstractions;
using HIS.Domain.Entities;

namespace HIS.Infrastructure.Persistence;

// ============================ Phase 2.4 — Emergency / ICU triage (§3.5) ============================
public sealed class EmergencyRepository : IEmergencyRepository
{
    private readonly ITenantConnectionFactory _f;
    public EmergencyRepository(ITenantConnectionFactory f) => _f = f;

    public async Task<long> InsertTriageAsync(EmergencyTriage t, CancellationToken ct = default)
    {
        using var c = await _f.OpenMasterAsync(ct);
        const string sql = @"
INSERT INTO clinical.EmergencyTriage
    (BranchId, PatientId, ArrivedUtc, Category, IsMlc, Notes, Status,
     ChiefComplaint, ArrivalMode, TriageLevel, PainScore, GcsTotal,
     TempF, Pulse, BpSystolic, BpDiastolic, Spo2, RespRate, Grbs, AttendingDoctorId)
VALUES
    (@BranchId, @PatientId, @ArrivedUtc, @Category, @IsMlc, @Notes, @Status,
     @ChiefComplaint, @ArrivalMode, @TriageLevel, @PainScore, @GcsTotal,
     @TempF, @Pulse, @BpSystolic, @BpDiastolic, @Spo2, @RespRate, @Grbs, @AttendingDoctorId);
SELECT CAST(SCOPE_IDENTITY() AS BIGINT);";
        return await c.QuerySingleAsync<long>(new CommandDefinition(sql, t, cancellationToken: ct));
    }

    public async Task<EmergencyTriage?> GetTriageAsync(long triageId, CancellationToken ct = default)
    {
        using var c = await _f.OpenMasterAsync(ct);
        return await c.QuerySingleOrDefaultAsync<EmergencyTriage>(new CommandDefinition(
            "SELECT * FROM clinical.EmergencyTriage WHERE TriageId = @triageId", new { triageId }, cancellationToken: ct));
    }

    public async Task SetTriageStatusAsync(long triageId, string status, CancellationToken ct = default)
    {
        using var c = await _f.OpenMasterAsync(ct);
        await c.ExecuteAsync(new CommandDefinition(
            "UPDATE clinical.EmergencyTriage SET Status = @status WHERE TriageId = @triageId",
            new { triageId, status }, cancellationToken: ct));
    }

    public async Task<int?> GetDoctorIdByCodeAsync(string code, CancellationToken ct = default)
    {
        using var c = await _f.OpenMasterAsync(ct);
        return await c.QuerySingleOrDefaultAsync<int?>(new CommandDefinition(
            "SELECT DoctorId FROM master.Doctor WHERE Code = @code AND IsActive = 1", new { code }, cancellationToken: ct));
    }

    public async Task DisposeAsync(long triageId, string status, long? admissionId, CancellationToken ct = default)
    {
        using var c = await _f.OpenMasterAsync(ct);
        await c.ExecuteAsync(new CommandDefinition(
            @"UPDATE clinical.EmergencyTriage
              SET Status = @status, AdmissionId = @admissionId, DisposedUtc = SYSUTCDATETIME()
              WHERE TriageId = @triageId",
            new { triageId, status, admissionId }, cancellationToken: ct));
    }

    public async Task<IReadOnlyList<(long, string?, string?, string, byte?, string?, string?, bool, string, DateTime)>> GetBoardAsync(
        int branchId, IReadOnlyList<string> severityOrder, CancellationToken ct = default)
    {
        using var c = await _f.OpenMasterAsync(ct);
        var rows = (await c.QueryAsync<(long, string?, string?, string, byte?, string?, string?, bool, string, DateTime)>(new CommandDefinition(
            @"SELECT t.TriageId, p.FullName AS Patient, p.Uhid, t.Category, t.TriageLevel,
                     t.ChiefComplaint, t.ArrivalMode, t.IsMlc, t.Status, t.ArrivedUtc
              FROM clinical.EmergencyTriage t
              LEFT JOIN patient.Patient p ON p.PatientId = t.PatientId
              WHERE t.BranchId = @branchId
                AND CAST(t.ArrivedUtc AS DATE) = CAST(SYSUTCDATETIME() AS DATE)",
            new { branchId }, cancellationToken: ct))).ToList();

        // Sort by acuity: numeric TriageLevel first (1=most urgent), else config category order; then arrival.
        int Rank((long, string?, string?, string, byte?, string?, string?, bool, string, DateTime) r)
        {
            if (r.Item5 is byte lvl) return lvl;               // TriageLevel 1..5
            for (var k = 0; k < severityOrder.Count; k++)      // fall back to config colour order
                if (string.Equals(severityOrder[k], r.Item4, StringComparison.OrdinalIgnoreCase)) return k + 1;
            return int.MaxValue;
        }
        return rows.OrderBy(Rank).ThenBy(r => r.Item10).ToList();
    }
}

// ============================ ICU monitoring flowsheet (§3.6) ============================
public sealed class IcuRepository : IIcuRepository
{
    private readonly ITenantConnectionFactory _f;
    public IcuRepository(ITenantConnectionFactory f) => _f = f;

    public async Task<IReadOnlyList<(long, string, string, string, string, string?)>> GetIcuAdmissionsAsync(int branchId, CancellationToken ct = default)
    {
        using var c = await _f.OpenMasterAsync(ct);
        return (await c.QueryAsync<(long, string, string, string, string, string?)>(new CommandDefinition(
            @"SELECT a.AdmissionId, ISNULL(p.FullName,'') AS Patient, ISNULL(p.Uhid,'') AS Uhid,
                     ISNULL(w.Name,'') AS Ward, ISNULL(b.BedNo,'') AS BedNo, d.Name AS Consultant
              FROM clinical.Admission a
              INNER JOIN patient.Patient p ON p.PatientId = a.PatientId
              LEFT JOIN master.Bed b ON b.BedId = a.BedId
              LEFT JOIN master.Ward w ON w.WardId = b.WardId
              LEFT JOIN master.Doctor d ON d.DoctorId = a.ConsultantId
              WHERE a.BranchId = @branchId AND a.Status = 'Admitted'
                    AND (w.Name LIKE '%ICU%' OR w.Name LIKE '%HDU%' OR w.Name LIKE '%Critical%')
              ORDER BY w.Name, b.BedNo", new { branchId }, cancellationToken: ct))).ToList();
    }

    public async Task<long> InsertObservationAsync(IcuObservation o, CancellationToken ct = default)
    {
        using var c = await _f.OpenMasterAsync(ct);
        const string sql = @"
INSERT INTO clinical.IcuObservation
    (AdmissionId, RecordedUtc, HeartRate, BpSystolic, BpDiastolic, Map, Spo2, RespRate, TempF,
     Cvp, EtCo2, Fio2, GcsTotal, PainScore, UrineOutputMl, BloodSugar, VentMode, Notes, RecordedById)
VALUES
    (@AdmissionId, @RecordedUtc, @HeartRate, @BpSystolic, @BpDiastolic, @Map, @Spo2, @RespRate, @TempF,
     @Cvp, @EtCo2, @Fio2, @GcsTotal, @PainScore, @UrineOutputMl, @BloodSugar, @VentMode, @Notes, @RecordedById);
SELECT CAST(SCOPE_IDENTITY() AS BIGINT);";
        return await c.QuerySingleAsync<long>(new CommandDefinition(sql, o, cancellationToken: ct));
    }

    public async Task<IReadOnlyList<IcuObservation>> GetFlowsheetAsync(long admissionId, CancellationToken ct = default)
    {
        using var c = await _f.OpenMasterAsync(ct);
        return (await c.QueryAsync<IcuObservation>(new CommandDefinition(
            "SELECT * FROM clinical.IcuObservation WHERE AdmissionId = @admissionId ORDER BY RecordedUtc DESC, IcuObservationId DESC",
            new { admissionId }, cancellationToken: ct))).ToList();
    }
}

// ============================ Phase 2.5 — Nursing & patient care (§3.13) ============================
public sealed class NursingRepository : INursingRepository
{
    private readonly ITenantConnectionFactory _f;
    public NursingRepository(ITenantConnectionFactory f) => _f = f;

    public async Task<bool> AdmissionExistsAsync(int branchId, long admissionId, CancellationToken ct = default)
    {
        using var c = await _f.OpenMasterAsync(ct);
        return await c.ExecuteScalarAsync<int>(new CommandDefinition(
            "SELECT COUNT(1) FROM clinical.Admission WHERE AdmissionId = @admissionId AND BranchId = @branchId",
            new { admissionId, branchId }, cancellationToken: ct)) > 0;
    }

    public async Task<long> InsertNoteAsync(NursingNote n, CancellationToken ct = default)
    {
        using var c = await _f.OpenMasterAsync(ct);
        const string sql = @"
INSERT INTO clinical.NursingNote (AdmissionId, RecordedUtc, NoteType, Note)
VALUES (@AdmissionId, @RecordedUtc, @NoteType, @Note);
SELECT CAST(SCOPE_IDENTITY() AS BIGINT);";
        return await c.QuerySingleAsync<long>(new CommandDefinition(sql, n, cancellationToken: ct));
    }

    public async Task<IReadOnlyList<(long, string?, string?, DateTime)>> GetNotesAsync(long admissionId, CancellationToken ct = default)
    {
        using var c = await _f.OpenMasterAsync(ct);
        var rows = await c.QueryAsync<(long, string?, string?, DateTime)>(new CommandDefinition(
            @"SELECT NoteId, NoteType, Note, RecordedUtc FROM clinical.NursingNote
              WHERE AdmissionId = @admissionId ORDER BY RecordedUtc DESC, NoteId DESC",
            new { admissionId }, cancellationToken: ct));
        return rows.ToList();
    }
}

// ============================ Phase 5.1 — Operation Theatre (§3.12) ============================
public sealed class OtRepository : IOtRepository
{
    private readonly ITenantConnectionFactory _f;
    public OtRepository(ITenantConnectionFactory f) => _f = f;

    public async Task<int?> GetDoctorIdByCodeAsync(string code, CancellationToken ct = default)
    {
        using var c = await _f.OpenMasterAsync(ct);
        return await c.QuerySingleOrDefaultAsync<int?>(new CommandDefinition(
            "SELECT DoctorId FROM master.Doctor WHERE Code = @code AND IsActive = 1", new { code }, cancellationToken: ct));
    }

    public async Task<long> InsertScheduleAsync(OtSchedule s, CancellationToken ct = default)
    {
        using var c = await _f.OpenMasterAsync(ct);
        // Column is Procedure_ (reserved word avoided in schema); entity property is Procedure.
        const string sql = @"
INSERT INTO clinical.OtSchedule (BranchId, PatientId, SurgeonId, Theatre, ScheduledUtc, Procedure_, PostOpNotes, Status)
VALUES (@BranchId, @PatientId, @SurgeonId, @Theatre, @ScheduledUtc, @Procedure, @PostOpNotes, @Status);
SELECT CAST(SCOPE_IDENTITY() AS BIGINT);";
        return await c.QuerySingleAsync<long>(new CommandDefinition(sql, s, cancellationToken: ct));
    }

    public async Task<OtSchedule?> GetScheduleAsync(long otId, CancellationToken ct = default)
    {
        using var c = await _f.OpenMasterAsync(ct);
        return await c.QuerySingleOrDefaultAsync<OtSchedule>(new CommandDefinition(
            @"SELECT OtId, BranchId, PatientId, SurgeonId, Theatre, ScheduledUtc,
                     Procedure_ AS [Procedure], PostOpNotes, Status
              FROM clinical.OtSchedule WHERE OtId = @otId", new { otId }, cancellationToken: ct));
    }

    public async Task SetStatusAsync(long otId, string status, CancellationToken ct = default)
    {
        using var c = await _f.OpenMasterAsync(ct);
        await c.ExecuteAsync(new CommandDefinition(
            "UPDATE clinical.OtSchedule SET Status = @status WHERE OtId = @otId",
            new { otId, status }, cancellationToken: ct));
    }

    public async Task CompleteAsync(long otId, string? postOpNotes, CancellationToken ct = default)
    {
        using var c = await _f.OpenMasterAsync(ct);
        await c.ExecuteAsync(new CommandDefinition(
            "UPDATE clinical.OtSchedule SET Status = 'Completed', PostOpNotes = @postOpNotes WHERE OtId = @otId",
            new { otId, postOpNotes }, cancellationToken: ct));
    }

    public async Task<IReadOnlyList<(long, string, string?, string?, string?, DateTime, string)>> GetBoardAsync(int branchId, CancellationToken ct = default)
    {
        using var c = await _f.OpenMasterAsync(ct);
        var rows = await c.QueryAsync<(long, string, string?, string?, string?, DateTime, string)>(new CommandDefinition(
            @"SELECT TOP 100 o.OtId, p.FullName AS Patient, d.Name AS Surgeon, o.Theatre,
                     o.Procedure_ AS [Procedure], o.ScheduledUtc, o.Status
              FROM clinical.OtSchedule o
              INNER JOIN patient.Patient p ON p.PatientId = o.PatientId
              LEFT JOIN master.Doctor d ON d.DoctorId = o.SurgeonId
              WHERE o.BranchId = @branchId
              ORDER BY o.ScheduledUtc DESC", new { branchId }, cancellationToken: ct));
        return rows.ToList();
    }
}
