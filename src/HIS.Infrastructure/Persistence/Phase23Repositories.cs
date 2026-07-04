using Dapper;
using HIS.Application.Abstractions;
using HIS.Domain.Entities;
using HIS.Shared.Context;

namespace HIS.Infrastructure.Persistence;

// L1.8.5 cutover: Admission is longitudinal (master DB); LIS/Radiology/BloodBank are
// fiscal-scoped (current-FY DB). FY repos resolve patient names via an app-side
// second query against the master DB (D8 two-step) — no cross-database joins.

public sealed class AdmissionRepository : IAdmissionRepository
{
    private readonly ITenantConnectionFactory _f;
    private readonly ITenantContext _tenant;
    public AdmissionRepository(ITenantConnectionFactory f, ITenantContext tenant) { _f = f; _tenant = tenant; }

    private sealed record BedRow(int BedId, string Status);

    public async Task<int?> GetDoctorIdByCodeAsync(string code, CancellationToken ct = default)
    {
        using var c = await _f.OpenMasterAsync(ct);
        return await c.QuerySingleOrDefaultAsync<int?>(new CommandDefinition(
            "SELECT DoctorId FROM master.Doctor WHERE Code = @code AND IsActive = 1", new { code }, cancellationToken: ct));
    }

    public async Task<(int BedId, string Status)?> GetBedByNoAsync(int branchId, string bedNo, CancellationToken ct = default)
    {
        using var c = await _f.OpenMasterAsync(ct);
        var row = await c.QuerySingleOrDefaultAsync<BedRow>(new CommandDefinition(
            @"SELECT b.BedId, b.Status FROM master.Bed b
              INNER JOIN master.Ward w ON w.WardId = b.WardId
              WHERE w.BranchId = @branchId AND b.BedNo = @bedNo", new { branchId, bedNo }, cancellationToken: ct));
        return row is null ? null : (row.BedId, row.Status);
    }

    public async Task SetBedStatusAsync(int bedId, string status, CancellationToken ct = default)
    {
        using var c = await _f.OpenMasterAsync(ct);
        await c.ExecuteAsync(new CommandDefinition(
            "UPDATE master.Bed SET Status = @status WHERE BedId = @bedId", new { bedId, status }, cancellationToken: ct));
    }

    public async Task<string> NextAdmissionNoAsync(int branchId, CancellationToken ct = default)
    {
        using var c = await _f.OpenMasterAsync(ct);
        return await c.QuerySingleAsync<string>(new CommandDefinition(
            "EXEC [proc].usp_NextDocNo @BranchId=@branchId, @DocType='IPD', @Prefix='IPD', @FyCode=@fy",
            new { branchId, fy = _tenant.FiscalYearCode ?? "" }, cancellationToken: ct));
    }

    public async Task<long> InsertAsync(Admission a, CancellationToken ct = default)
    {
        using var c = await _f.OpenMasterAsync(ct);
        const string sql = @"
INSERT INTO clinical.Admission (AdmissionNo, BranchId, PatientId, BedId, ConsultantId, AdmittedUtc, AdmissionType, PaymentClass, ProvisionalIcd10, EstStayDays, Status)
VALUES (@AdmissionNo, @BranchId, @PatientId, @BedId, @ConsultantId, @AdmittedUtc, @AdmissionType, @PaymentClass, @ProvisionalIcd10, @EstStayDays, @Status);
SELECT CAST(SCOPE_IDENTITY() AS BIGINT);";
        return await c.QuerySingleAsync<long>(new CommandDefinition(sql, a, cancellationToken: ct));
    }

    public async Task<Admission?> GetAsync(long admissionId, CancellationToken ct = default)
    {
        using var c = await _f.OpenMasterAsync(ct);
        return await c.QuerySingleOrDefaultAsync<Admission>(new CommandDefinition(
            "SELECT * FROM clinical.Admission WHERE AdmissionId = @admissionId", new { admissionId }, cancellationToken: ct));
    }

    public async Task UpdateBedAsync(long admissionId, int? bedId, CancellationToken ct = default)
    {
        using var c = await _f.OpenMasterAsync(ct);
        await c.ExecuteAsync(new CommandDefinition(
            "UPDATE clinical.Admission SET BedId = @bedId WHERE AdmissionId = @admissionId", new { admissionId, bedId }, cancellationToken: ct));
    }

    public async Task InsertTransferAsync(BedTransfer t, CancellationToken ct = default)
    {
        using var c = await _f.OpenMasterAsync(ct);
        const string sql = @"
INSERT INTO clinical.BedTransfer (AdmissionId, FromBedId, ToBedId, TransferUtc, Reason)
VALUES (@AdmissionId, @FromBedId, @ToBedId, @TransferUtc, @Reason);";
        await c.ExecuteAsync(new CommandDefinition(sql, t, cancellationToken: ct));
    }

    public async Task DischargeAsync(long admissionId, string? summary, DateTime dischargedUtc, CancellationToken ct = default)
    {
        using var c = await _f.OpenMasterAsync(ct);
        await c.ExecuteAsync(new CommandDefinition(
            @"UPDATE clinical.Admission SET DischargedUtc = @dischargedUtc, DischargeSummary = @summary, Status = 'Discharged'
              WHERE AdmissionId = @admissionId", new { admissionId, summary, dischargedUtc }, cancellationToken: ct));
    }

    public async Task<IReadOnlyList<(string, string, string, string?)>> GetBedBoardAsync(int branchId, CancellationToken ct = default)
    {
        // All in the master DB (Bed/Ward/Admission/Patient) → intra-DB join.
        using var c = await _f.OpenMasterAsync(ct);
        var rows = await c.QueryAsync<(string, string, string, string?)>(new CommandDefinition(
            @"SELECT w.Name AS Ward, b.BedNo, b.Status, p.FullName AS Occupant
              FROM master.Bed b
              INNER JOIN master.Ward w ON w.WardId = b.WardId
              LEFT JOIN clinical.Admission a ON a.BedId = b.BedId AND a.Status = 'Admitted'
              LEFT JOIN patient.Patient p ON p.PatientId = a.PatientId
              WHERE w.BranchId = @branchId
              ORDER BY w.Name, b.BedNo", new { branchId }, cancellationToken: ct));
        return rows.ToList();
    }

    public async Task<IReadOnlyList<(long, string, string, string, string, string, string?, DateTime)>> GetAdmittedPatientsAsync(int branchId, CancellationToken ct = default)
    {
        using var c = await _f.OpenMasterAsync(ct);
        var rows = await c.QueryAsync<(long, string, string, string, string, string, string?, DateTime)>(new CommandDefinition(
            @"SELECT a.AdmissionId,
                     a.AdmissionNo,
                     ISNULL(p.FullName,'') AS Patient,
                     ISNULL(p.Uhid,'')     AS Uhid,
                     ISNULL(w.Name,'')     AS Ward,
                     ISNULL(b.BedNo,'')    AS BedNo,
                     d.Name                AS Consultant,
                     a.AdmittedUtc
              FROM clinical.Admission a
              INNER JOIN patient.Patient p ON p.PatientId = a.PatientId
              LEFT JOIN master.Bed b ON b.BedId = a.BedId
              LEFT JOIN master.Ward w ON w.WardId = b.WardId
              LEFT JOIN master.Doctor d ON d.DoctorId = a.ConsultantId
              WHERE a.BranchId = @branchId AND a.Status = 'Admitted'
              ORDER BY a.AdmittedUtc DESC", new { branchId }, cancellationToken: ct));
        return rows.ToList();
    }
}

/// <summary>Shared helper: resolve patient names from the master DB for a set of ids (D8 two-step).</summary>
internal static class MasterLookup
{
    public static async Task<Dictionary<long, string>> PatientNamesAsync(
        ITenantConnectionFactory f, IEnumerable<long> patientIds, CancellationToken ct)
    {
        var ids = patientIds.Where(i => i > 0).Distinct().ToArray();
        if (ids.Length == 0) return new();
        using var c = await f.OpenMasterAsync(ct);
        var rows = await c.QueryAsync<(long PatientId, string FullName)>(new CommandDefinition(
            "SELECT PatientId, FullName FROM patient.Patient WHERE PatientId IN @ids", new { ids }, cancellationToken: ct));
        return rows.ToDictionary(r => r.PatientId, r => r.FullName);
    }

    public static async Task<Dictionary<int, string>> PayerNamesAsync(
        ITenantConnectionFactory f, IEnumerable<int> payerIds, CancellationToken ct)
    {
        var ids = payerIds.Where(i => i > 0).Distinct().ToArray();
        if (ids.Length == 0) return new();
        using var c = await f.OpenMasterAsync(ct);
        var rows = await c.QueryAsync<(int PayerId, string Name)>(new CommandDefinition(
            "SELECT PayerId, Name FROM master.Payer WHERE PayerId IN @ids", new { ids }, cancellationToken: ct));
        return rows.ToDictionary(r => r.PayerId, r => r.Name);
    }

    public static async Task<Dictionary<int, string>> DoctorNamesAsync(
        ITenantConnectionFactory f, IEnumerable<int?> doctorIds, CancellationToken ct)
    {
        var ids = doctorIds.Where(i => i.HasValue).Select(i => i!.Value).Distinct().ToArray();
        if (ids.Length == 0) return new();
        using var c = await f.OpenMasterAsync(ct);
        var rows = await c.QueryAsync<(int DoctorId, string Name)>(new CommandDefinition(
            "SELECT DoctorId, Name FROM master.Doctor WHERE DoctorId IN @ids", new { ids }, cancellationToken: ct));
        return rows.ToDictionary(r => r.DoctorId, r => r.Name);
    }

    /// <summary>Active staff in a branch (master DB), keyed by StaffId → (EmployeeCode, FullName).</summary>
    public static async Task<Dictionary<long, (string Code, string Name)>> StaffInBranchAsync(
        ITenantConnectionFactory f, int branchId, CancellationToken ct)
    {
        using var c = await f.OpenMasterAsync(ct);
        var rows = await c.QueryAsync<(long StaffId, string EmployeeCode, string FullName)>(new CommandDefinition(
            "SELECT StaffId, EmployeeCode, FullName FROM master.Staff WHERE BranchId = @branchId AND IsActive = 1",
            new { branchId }, cancellationToken: ct));
        return rows.ToDictionary(r => r.StaffId, r => (r.EmployeeCode, r.FullName));
    }
}

public sealed class LisRepository : ILisRepository
{
    private readonly ITenantConnectionFactory _f;
    private readonly ITenantContext _tenant;
    public LisRepository(ITenantConnectionFactory f, ITenantContext tenant) { _f = f; _tenant = tenant; }

    public async Task<string> NextBarcodeAsync(int branchId, CancellationToken ct = default)
    {
        using var c = await _f.OpenDataAsync(ct);
        return await c.QuerySingleAsync<string>(new CommandDefinition(
            "EXEC [proc].usp_NextDocNo @BranchId=@branchId, @DocType='LAB', @Prefix='LB', @FyCode=@fy",
            new { branchId, fy = _tenant.FiscalYearCode ?? "" }, cancellationToken: ct));
    }

    public async Task<long> CreateOrderAsync(LabOrder o, CancellationToken ct = default)
    {
        using var c = await _f.OpenDataAsync(ct);
        const string sql = @"
INSERT INTO diagnostics.LabOrder (Barcode, EncounterId, PatientId, TestName, CollectedUtc, Status)
VALUES (@Barcode, @EncounterId, @PatientId, @TestName, @CollectedUtc, @Status);
SELECT CAST(SCOPE_IDENTITY() AS BIGINT);";
        return await c.QuerySingleAsync<long>(new CommandDefinition(sql, o, cancellationToken: ct));
    }

    public async Task<IReadOnlyList<(string, string, string, string, long)>> GetWorklistAsync(int branchId, CancellationToken ct = default)
    {
        using var c = await _f.OpenDataAsync(ct);
        var orders = (await c.QueryAsync<(long LabOrderId, string Barcode, long PatientId, string TestName, string Status)>(new CommandDefinition(
            @"SELECT TOP 100 LabOrderId, Barcode, PatientId, TestName, Status
              FROM diagnostics.LabOrder ORDER BY LabOrderId DESC", cancellationToken: ct))).ToList();
        var names = await MasterLookup.PatientNamesAsync(_f, orders.Select(o => o.PatientId), ct);
        return orders.Select(o => (o.Barcode, names.GetValueOrDefault(o.PatientId, ""), o.TestName, o.Status, o.LabOrderId)).ToList();
    }

    public async Task AddResultAsync(LabResult r, CancellationToken ct = default)
    {
        using var c = await _f.OpenDataAsync(ct);
        const string sql = @"
INSERT INTO diagnostics.LabResult (LabOrderId, Parameter, ResultValue, Unit, ReferenceRange, Flag, ValidatedUtc)
VALUES (@LabOrderId, @Parameter, @ResultValue, @Unit, @ReferenceRange, @Flag, @ValidatedUtc);";
        await c.ExecuteAsync(new CommandDefinition(sql, r, cancellationToken: ct));
    }

    public async Task SetOrderStatusAsync(long labOrderId, string status, CancellationToken ct = default)
    {
        using var c = await _f.OpenDataAsync(ct);
        await c.ExecuteAsync(new CommandDefinition(
            "UPDATE diagnostics.LabOrder SET Status = @status WHERE LabOrderId = @labOrderId", new { labOrderId, status }, cancellationToken: ct));
    }

    public async Task<IReadOnlyList<LabResult>> GetResultsAsync(long labOrderId, CancellationToken ct = default)
    {
        using var c = await _f.OpenDataAsync(ct);
        return (await c.QueryAsync<LabResult>(new CommandDefinition(
            "SELECT * FROM diagnostics.LabResult WHERE LabOrderId = @labOrderId ORDER BY ResultId", new { labOrderId }, cancellationToken: ct))).ToList();
    }
}

public sealed class RadiologyRepository : IRadiologyRepository
{
    private readonly ITenantConnectionFactory _f;
    public RadiologyRepository(ITenantConnectionFactory f) => _f = f;

    public async Task<long> CreateOrderAsync(RadiologyOrder o, CancellationToken ct = default)
    {
        using var c = await _f.OpenDataAsync(ct);
        const string sql = @"
INSERT INTO diagnostics.RadiologyOrder (PatientId, Modality, StudyName, ScheduledUtc, ReportUrl, ReportedByDoctorId, IsPcPndtRegulated, Status)
VALUES (@PatientId, @Modality, @StudyName, @ScheduledUtc, @ReportUrl, @ReportedByDoctorId, @IsPcPndtRegulated, @Status);
SELECT CAST(SCOPE_IDENTITY() AS BIGINT);";
        return await c.QuerySingleAsync<long>(new CommandDefinition(sql, o, cancellationToken: ct));
    }

    public async Task SetReportAsync(long radOrderId, string status, string? reportUrl, CancellationToken ct = default)
    {
        using var c = await _f.OpenDataAsync(ct);
        await c.ExecuteAsync(new CommandDefinition(
            "UPDATE diagnostics.RadiologyOrder SET Status = @status, ReportUrl = @reportUrl WHERE RadOrderId = @radOrderId",
            new { radOrderId, status, reportUrl }, cancellationToken: ct));
    }

    public async Task<IReadOnlyList<(long, string, string?, string, string)>> GetWorklistAsync(int branchId, CancellationToken ct = default)
    {
        using var c = await _f.OpenDataAsync(ct);
        var orders = (await c.QueryAsync<(long RadOrderId, string Modality, string? StudyName, long PatientId, string Status)>(new CommandDefinition(
            @"SELECT TOP 100 RadOrderId, Modality, StudyName, PatientId, Status
              FROM diagnostics.RadiologyOrder ORDER BY RadOrderId DESC", cancellationToken: ct))).ToList();
        var names = await MasterLookup.PatientNamesAsync(_f, orders.Select(o => o.PatientId), ct);
        return orders.Select(o => (o.RadOrderId, o.Modality, o.StudyName, names.GetValueOrDefault(o.PatientId, ""), o.Status)).ToList();
    }
}

public sealed class BloodBankRepository : IBloodBankRepository
{
    private readonly ITenantConnectionFactory _f;
    public BloodBankRepository(ITenantConnectionFactory f) => _f = f;

    public async Task<IReadOnlyList<BloodStock>> GetStockAsync(int branchId, CancellationToken ct = default)
    {
        using var c = await _f.OpenDataAsync(ct);
        return (await c.QueryAsync<BloodStock>(new CommandDefinition(
            @"SELECT * FROM diagnostics.BloodStock WHERE BranchId = @branchId ORDER BY BloodGroup", new { branchId }, cancellationToken: ct))).ToList();
    }

    public async Task<long> CreateRequestAsync(BloodRequest r, CancellationToken ct = default)
    {
        using var c = await _f.OpenDataAsync(ct);
        const string sql = @"
INSERT INTO diagnostics.BloodRequest (BranchId, PatientId, BloodGroup, Units, IsEmergency, RequestedUtc, Status)
VALUES (@BranchId, @PatientId, @BloodGroup, @Units, @IsEmergency, @RequestedUtc, @Status);
SELECT CAST(SCOPE_IDENTITY() AS BIGINT);";
        return await c.QuerySingleAsync<long>(new CommandDefinition(sql, r, cancellationToken: ct));
    }

    public async Task<int> GetAvailableUnitsAsync(int branchId, string bloodGroup, CancellationToken ct = default)
    {
        using var c = await _f.OpenDataAsync(ct);
        return await c.ExecuteScalarAsync<int>(new CommandDefinition(
            "SELECT ISNULL(SUM(Units),0) FROM diagnostics.BloodStock WHERE BranchId = @branchId AND BloodGroup = @bloodGroup",
            new { branchId, bloodGroup }, cancellationToken: ct));
    }
}
