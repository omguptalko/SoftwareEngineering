using Dapper;
using HIS.Application.Abstractions;
using HIS.Domain.Entities;

namespace HIS.Infrastructure.Persistence;

public sealed class AdmissionRepository : IAdmissionRepository
{
    private readonly IDbConnectionFactory _f;
    public AdmissionRepository(IDbConnectionFactory f) => _f = f;

    private sealed record BedRow(int BedId, string Status);

    public async Task<int?> GetDoctorIdByCodeAsync(string code, CancellationToken ct = default)
    {
        using var c = await _f.CreateOpenConnectionAsync(ct);
        return await c.QuerySingleOrDefaultAsync<int?>(new CommandDefinition(
            "SELECT DoctorId FROM dbo.Doctor WHERE Code = @code AND IsActive = 1", new { code }, cancellationToken: ct));
    }

    public async Task<(int BedId, string Status)?> GetBedByNoAsync(int branchId, string bedNo, CancellationToken ct = default)
    {
        using var c = await _f.CreateOpenConnectionAsync(ct);
        var row = await c.QuerySingleOrDefaultAsync<BedRow>(new CommandDefinition(
            @"SELECT b.BedId, b.Status FROM dbo.Bed b
              INNER JOIN dbo.Ward w ON w.WardId = b.WardId
              WHERE w.BranchId = @branchId AND b.BedNo = @bedNo", new { branchId, bedNo }, cancellationToken: ct));
        return row is null ? null : (row.BedId, row.Status);
    }

    public async Task SetBedStatusAsync(int bedId, string status, CancellationToken ct = default)
    {
        using var c = await _f.CreateOpenConnectionAsync(ct);
        await c.ExecuteAsync(new CommandDefinition(
            "UPDATE dbo.Bed SET Status = @status WHERE BedId = @bedId", new { bedId, status }, cancellationToken: ct));
    }

    public async Task<string> NextAdmissionNoAsync(int branchId, CancellationToken ct = default)
    {
        using var c = await _f.CreateOpenConnectionAsync(ct);
        return await c.QuerySingleAsync<string>(new CommandDefinition(
            "EXEC dbo.usp_NextDocNo @BranchId=@branchId, @DocType='IPD', @Prefix='IPD'", new { branchId }, cancellationToken: ct));
    }

    public async Task<long> InsertAsync(Admission a, CancellationToken ct = default)
    {
        using var c = await _f.CreateOpenConnectionAsync(ct);
        const string sql = @"
INSERT INTO dbo.Admission (AdmissionNo, BranchId, PatientId, BedId, ConsultantId, AdmittedUtc, AdmissionType, PaymentClass, ProvisionalIcd10, EstStayDays, Status)
VALUES (@AdmissionNo, @BranchId, @PatientId, @BedId, @ConsultantId, @AdmittedUtc, @AdmissionType, @PaymentClass, @ProvisionalIcd10, @EstStayDays, @Status);
SELECT CAST(SCOPE_IDENTITY() AS BIGINT);";
        return await c.QuerySingleAsync<long>(new CommandDefinition(sql, a, cancellationToken: ct));
    }

    public async Task<Admission?> GetAsync(long admissionId, CancellationToken ct = default)
    {
        using var c = await _f.CreateOpenConnectionAsync(ct);
        return await c.QuerySingleOrDefaultAsync<Admission>(new CommandDefinition(
            "SELECT * FROM dbo.Admission WHERE AdmissionId = @admissionId", new { admissionId }, cancellationToken: ct));
    }

    public async Task UpdateBedAsync(long admissionId, int? bedId, CancellationToken ct = default)
    {
        using var c = await _f.CreateOpenConnectionAsync(ct);
        await c.ExecuteAsync(new CommandDefinition(
            "UPDATE dbo.Admission SET BedId = @bedId WHERE AdmissionId = @admissionId", new { admissionId, bedId }, cancellationToken: ct));
    }

    public async Task InsertTransferAsync(BedTransfer t, CancellationToken ct = default)
    {
        using var c = await _f.CreateOpenConnectionAsync(ct);
        const string sql = @"
INSERT INTO dbo.BedTransfer (AdmissionId, FromBedId, ToBedId, TransferUtc, Reason)
VALUES (@AdmissionId, @FromBedId, @ToBedId, @TransferUtc, @Reason);";
        await c.ExecuteAsync(new CommandDefinition(sql, t, cancellationToken: ct));
    }

    public async Task DischargeAsync(long admissionId, string? summary, DateTime dischargedUtc, CancellationToken ct = default)
    {
        using var c = await _f.CreateOpenConnectionAsync(ct);
        await c.ExecuteAsync(new CommandDefinition(
            @"UPDATE dbo.Admission SET DischargedUtc = @dischargedUtc, DischargeSummary = @summary, Status = 'Discharged'
              WHERE AdmissionId = @admissionId", new { admissionId, summary, dischargedUtc }, cancellationToken: ct));
    }

    public async Task<IReadOnlyList<(string, string, string, string?)>> GetBedBoardAsync(int branchId, CancellationToken ct = default)
    {
        using var c = await _f.CreateOpenConnectionAsync(ct);
        var rows = await c.QueryAsync<(string, string, string, string?)>(new CommandDefinition(
            @"SELECT w.Name AS Ward, b.BedNo, b.Status, p.FullName AS Occupant
              FROM dbo.Bed b
              INNER JOIN dbo.Ward w ON w.WardId = b.WardId
              LEFT JOIN dbo.Admission a ON a.BedId = b.BedId AND a.Status = 'Admitted'
              LEFT JOIN dbo.Patient p ON p.PatientId = a.PatientId
              WHERE w.BranchId = @branchId
              ORDER BY w.Name, b.BedNo", new { branchId }, cancellationToken: ct));
        return rows.ToList();
    }
}

public sealed class LisRepository : ILisRepository
{
    private readonly IDbConnectionFactory _f;
    public LisRepository(IDbConnectionFactory f) => _f = f;

    public async Task<string> NextBarcodeAsync(int branchId, CancellationToken ct = default)
    {
        using var c = await _f.CreateOpenConnectionAsync(ct);
        return await c.QuerySingleAsync<string>(new CommandDefinition(
            "EXEC dbo.usp_NextDocNo @BranchId=@branchId, @DocType='LAB', @Prefix='LB'", new { branchId }, cancellationToken: ct));
    }

    public async Task<long> CreateOrderAsync(LabOrder o, CancellationToken ct = default)
    {
        using var c = await _f.CreateOpenConnectionAsync(ct);
        const string sql = @"
INSERT INTO dbo.LabOrder (Barcode, EncounterId, PatientId, TestName, CollectedUtc, Status)
VALUES (@Barcode, @EncounterId, @PatientId, @TestName, @CollectedUtc, @Status);
SELECT CAST(SCOPE_IDENTITY() AS BIGINT);";
        return await c.QuerySingleAsync<long>(new CommandDefinition(sql, o, cancellationToken: ct));
    }

    public async Task<IReadOnlyList<(string, string, string, string, long)>> GetWorklistAsync(int branchId, CancellationToken ct = default)
    {
        using var c = await _f.CreateOpenConnectionAsync(ct);
        var rows = await c.QueryAsync<(string, string, string, string, long)>(new CommandDefinition(
            @"SELECT TOP 100 lo.Barcode, p.FullName, lo.TestName, lo.Status, lo.LabOrderId
              FROM dbo.LabOrder lo INNER JOIN dbo.Patient p ON p.PatientId = lo.PatientId
              WHERE p.RegBranchId = @branchId
              ORDER BY lo.LabOrderId DESC", new { branchId }, cancellationToken: ct));
        return rows.ToList();
    }

    public async Task AddResultAsync(LabResult r, CancellationToken ct = default)
    {
        using var c = await _f.CreateOpenConnectionAsync(ct);
        const string sql = @"
INSERT INTO dbo.LabResult (LabOrderId, Parameter, ResultValue, Unit, ReferenceRange, Flag, ValidatedUtc)
VALUES (@LabOrderId, @Parameter, @ResultValue, @Unit, @ReferenceRange, @Flag, @ValidatedUtc);";
        await c.ExecuteAsync(new CommandDefinition(sql, r, cancellationToken: ct));
    }

    public async Task SetOrderStatusAsync(long labOrderId, string status, CancellationToken ct = default)
    {
        using var c = await _f.CreateOpenConnectionAsync(ct);
        await c.ExecuteAsync(new CommandDefinition(
            "UPDATE dbo.LabOrder SET Status = @status WHERE LabOrderId = @labOrderId", new { labOrderId, status }, cancellationToken: ct));
    }

    public async Task<IReadOnlyList<LabResult>> GetResultsAsync(long labOrderId, CancellationToken ct = default)
    {
        using var c = await _f.CreateOpenConnectionAsync(ct);
        return (await c.QueryAsync<LabResult>(new CommandDefinition(
            "SELECT * FROM dbo.LabResult WHERE LabOrderId = @labOrderId ORDER BY ResultId", new { labOrderId }, cancellationToken: ct))).ToList();
    }
}

public sealed class RadiologyRepository : IRadiologyRepository
{
    private readonly IDbConnectionFactory _f;
    public RadiologyRepository(IDbConnectionFactory f) => _f = f;

    public async Task<long> CreateOrderAsync(RadiologyOrder o, CancellationToken ct = default)
    {
        using var c = await _f.CreateOpenConnectionAsync(ct);
        const string sql = @"
INSERT INTO dbo.RadiologyOrder (PatientId, Modality, StudyName, ScheduledUtc, ReportUrl, ReportedByDoctorId, IsPcPndtRegulated, Status)
VALUES (@PatientId, @Modality, @StudyName, @ScheduledUtc, @ReportUrl, @ReportedByDoctorId, @IsPcPndtRegulated, @Status);
SELECT CAST(SCOPE_IDENTITY() AS BIGINT);";
        return await c.QuerySingleAsync<long>(new CommandDefinition(sql, o, cancellationToken: ct));
    }

    public async Task<IReadOnlyList<(string, string?, string, string)>> GetWorklistAsync(int branchId, CancellationToken ct = default)
    {
        using var c = await _f.CreateOpenConnectionAsync(ct);
        var rows = await c.QueryAsync<(string, string?, string, string)>(new CommandDefinition(
            @"SELECT TOP 100 ro.Modality, ro.StudyName, p.FullName, ro.Status
              FROM dbo.RadiologyOrder ro INNER JOIN dbo.Patient p ON p.PatientId = ro.PatientId
              WHERE p.RegBranchId = @branchId
              ORDER BY ro.RadOrderId DESC", new { branchId }, cancellationToken: ct));
        return rows.ToList();
    }
}

public sealed class BloodBankRepository : IBloodBankRepository
{
    private readonly IDbConnectionFactory _f;
    public BloodBankRepository(IDbConnectionFactory f) => _f = f;

    public async Task<IReadOnlyList<BloodStock>> GetStockAsync(int branchId, CancellationToken ct = default)
    {
        using var c = await _f.CreateOpenConnectionAsync(ct);
        return (await c.QueryAsync<BloodStock>(new CommandDefinition(
            @"SELECT bs.* FROM dbo.BloodStock bs INNER JOIN dbo.BloodGroup g ON g.Code = bs.BloodGroup
              WHERE bs.BranchId = @branchId ORDER BY g.SortOrder", new { branchId }, cancellationToken: ct))).ToList();
    }

    public async Task<long> CreateRequestAsync(BloodRequest r, CancellationToken ct = default)
    {
        using var c = await _f.CreateOpenConnectionAsync(ct);
        const string sql = @"
INSERT INTO dbo.BloodRequest (BranchId, PatientId, BloodGroup, Units, IsEmergency, RequestedUtc, Status)
VALUES (@BranchId, @PatientId, @BloodGroup, @Units, @IsEmergency, @RequestedUtc, @Status);
SELECT CAST(SCOPE_IDENTITY() AS BIGINT);";
        return await c.QuerySingleAsync<long>(new CommandDefinition(sql, r, cancellationToken: ct));
    }

    public async Task<int> GetAvailableUnitsAsync(int branchId, string bloodGroup, CancellationToken ct = default)
    {
        using var c = await _f.CreateOpenConnectionAsync(ct);
        return await c.ExecuteScalarAsync<int>(new CommandDefinition(
            "SELECT ISNULL(SUM(Units),0) FROM dbo.BloodStock WHERE BranchId = @branchId AND BloodGroup = @bloodGroup",
            new { branchId, bloodGroup }, cancellationToken: ct));
    }
}
