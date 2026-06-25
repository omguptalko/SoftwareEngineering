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
INSERT INTO clinical.EmergencyTriage (BranchId, PatientId, ArrivedUtc, Category, IsMlc, Notes, Status)
VALUES (@BranchId, @PatientId, @ArrivedUtc, @Category, @IsMlc, @Notes, @Status);
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

    public async Task<IReadOnlyList<(long, string?, string, bool, string, DateTime)>> GetBoardAsync(
        int branchId, IReadOnlyList<string> severityOrder, CancellationToken ct = default)
    {
        using var c = await _f.OpenMasterAsync(ct);
        var rows = (await c.QueryAsync<(long, string?, string, bool, string, DateTime)>(new CommandDefinition(
            @"SELECT t.TriageId, p.FullName AS Patient, t.Category, t.IsMlc, t.Status, t.ArrivedUtc
              FROM clinical.EmergencyTriage t
              LEFT JOIN patient.Patient p ON p.PatientId = t.PatientId
              WHERE t.BranchId = @branchId
                AND CAST(t.ArrivedUtc AS DATE) = CAST(SYSUTCDATETIME() AS DATE)",
            new { branchId }, cancellationToken: ct))).ToList();

        // Rank by config-driven severity order (e.g. Red → Yellow → Green), then by arrival.
        // Unknown categories sort last. Nothing about the order is hardcoded in SQL.
        int Rank(string cat)
        {
            var i = -1;
            for (var k = 0; k < severityOrder.Count; k++)
                if (string.Equals(severityOrder[k], cat, StringComparison.OrdinalIgnoreCase)) { i = k; break; }
            return i < 0 ? int.MaxValue : i;
        }
        return rows
            .OrderBy(r => Rank(r.Item3))
            .ThenBy(r => r.Item6)
            .ToList();
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
