using Dapper;
using HIS.Application.Abstractions;
using HIS.Domain.Entities;

namespace HIS.Infrastructure.Persistence;

public sealed class AmbulanceRepository : IAmbulanceRepository
{
    private readonly IDbConnectionFactory _f;
    public AmbulanceRepository(IDbConnectionFactory f) => _f = f;

    public async Task<IReadOnlyList<(int, string, string)>> GetAmbulancesAsync(int branchId, CancellationToken ct = default)
    {
        using var c = await _f.CreateOpenConnectionAsync(ct);
        return (await c.QueryAsync<(int, string, string)>(new CommandDefinition(
            "SELECT AmbulanceId, VehicleNo, Status FROM dbo.Ambulance WHERE BranchId = @branchId ORDER BY VehicleNo", new { branchId }, cancellationToken: ct))).ToList();
    }
    public async Task<int?> GetFirstAvailableAsync(int branchId, CancellationToken ct = default)
    {
        using var c = await _f.CreateOpenConnectionAsync(ct);
        return await c.QuerySingleOrDefaultAsync<int?>(new CommandDefinition(
            "SELECT TOP 1 AmbulanceId FROM dbo.Ambulance WHERE BranchId = @branchId AND Status = 'Available' ORDER BY AmbulanceId", new { branchId }, cancellationToken: ct));
    }
    public async Task<long> InsertDispatchAsync(AmbulanceDispatch d, CancellationToken ct = default)
    {
        using var c = await _f.CreateOpenConnectionAsync(ct);
        const string sql = @"
INSERT INTO dbo.AmbulanceDispatch (AmbulanceId, CallLoggedUtc, PickupLat, PickupLng, LastLat, LastLng, Status)
VALUES (@AmbulanceId, @CallLoggedUtc, @PickupLat, @PickupLng, @LastLat, @LastLng, @Status);
SELECT CAST(SCOPE_IDENTITY() AS BIGINT);";
        return await c.QuerySingleAsync<long>(new CommandDefinition(sql, d, cancellationToken: ct));
    }
    public async Task SetAmbulanceStatusAsync(int ambulanceId, string status, CancellationToken ct = default)
    {
        using var c = await _f.CreateOpenConnectionAsync(ct);
        await c.ExecuteAsync(new CommandDefinition("UPDATE dbo.Ambulance SET Status = @status WHERE AmbulanceId = @ambulanceId", new { ambulanceId, status }, cancellationToken: ct));
    }
    public async Task ArriveAsync(long dispatchId, decimal? lat, decimal? lng, CancellationToken ct = default)
    {
        using var c = await _f.CreateOpenConnectionAsync(ct);
        await c.ExecuteAsync(new CommandDefinition(
            "UPDATE dbo.AmbulanceDispatch SET ArrivedUtc = SYSUTCDATETIME(), LastLat = @lat, LastLng = @lng, Status = 'Arrived' WHERE DispatchId = @dispatchId",
            new { dispatchId, lat, lng }, cancellationToken: ct));
    }
    public async Task<int> GetDispatchAmbulanceAsync(long dispatchId, CancellationToken ct = default)
    {
        using var c = await _f.CreateOpenConnectionAsync(ct);
        return await c.QuerySingleAsync<int>(new CommandDefinition("SELECT AmbulanceId FROM dbo.AmbulanceDispatch WHERE DispatchId = @dispatchId", new { dispatchId }, cancellationToken: ct));
    }
    public async Task<IReadOnlyList<(long, string, string, string?, string)>> GetDispatchesAsync(int branchId, CancellationToken ct = default)
    {
        using var c = await _f.CreateOpenConnectionAsync(ct);
        return (await c.QueryAsync<(long, string, string, string?, string)>(new CommandDefinition(
            @"SELECT d.DispatchId, a.VehicleNo, CONVERT(varchar(16), d.CallLoggedUtc, 120) AS Logged,
                     CONVERT(varchar(16), d.ArrivedUtc, 120) AS Arrived, d.Status
              FROM dbo.AmbulanceDispatch d INNER JOIN dbo.Ambulance a ON a.AmbulanceId = d.AmbulanceId
              WHERE a.BranchId = @branchId ORDER BY d.DispatchId DESC", new { branchId }, cancellationToken: ct))).ToList();
    }
}

public sealed class StatutoryRepository : IStatutoryRepository
{
    private readonly IDbConnectionFactory _f;
    public StatutoryRepository(IDbConnectionFactory f) => _f = f;

    public async Task<long> InsertDietAsync(DietOrder d, CancellationToken ct = default)
    {
        using var c = await _f.CreateOpenConnectionAsync(ct);
        const string sql = @"INSERT INTO dbo.DietOrder (AdmissionId, DietType, OrderedUtc, Cost) VALUES (@AdmissionId, @DietType, @OrderedUtc, @Cost); SELECT CAST(SCOPE_IDENTITY() AS BIGINT);";
        return await c.QuerySingleAsync<long>(new CommandDefinition(sql, d, cancellationToken: ct));
    }
    public async Task<IReadOnlyList<(long, string, string, decimal?)>> GetDietAsync(int branchId, CancellationToken ct = default)
    {
        using var c = await _f.CreateOpenConnectionAsync(ct);
        return (await c.QueryAsync<(long, string, string, decimal?)>(new CommandDefinition(
            @"SELECT d.DietOrderId, ISNULL(p.FullName,'') AS Patient, d.DietType, d.Cost
              FROM dbo.DietOrder d INNER JOIN dbo.Admission a ON a.AdmissionId = d.AdmissionId
              LEFT JOIN dbo.Patient p ON p.PatientId = a.PatientId
              WHERE a.BranchId = @branchId ORDER BY d.DietOrderId DESC", new { branchId }, cancellationToken: ct))).ToList();
    }
    public async Task<long> InsertWasteBagAsync(WasteBag b, CancellationToken ct = default)
    {
        using var c = await _f.CreateOpenConnectionAsync(ct);
        const string sql = @"INSERT INTO dbo.WasteBag (BranchId, Barcode, ColourCode, WeightKg, GeneratedUtc) VALUES (@BranchId, @Barcode, @ColourCode, @WeightKg, @GeneratedUtc); SELECT CAST(SCOPE_IDENTITY() AS BIGINT);";
        return await c.QuerySingleAsync<long>(new CommandDefinition(sql, b, cancellationToken: ct));
    }
    public async Task HandoverWasteBagAsync(long bagId, CancellationToken ct = default)
    {
        using var c = await _f.CreateOpenConnectionAsync(ct);
        await c.ExecuteAsync(new CommandDefinition("UPDATE dbo.WasteBag SET CbwtfHandoverUtc = SYSUTCDATETIME() WHERE BagId = @bagId", new { bagId }, cancellationToken: ct));
    }
    public async Task<IReadOnlyList<(string, string, decimal?, bool)>> GetWasteBagsAsync(int branchId, CancellationToken ct = default)
    {
        using var c = await _f.CreateOpenConnectionAsync(ct);
        return (await c.QueryAsync<(string, string, decimal?, bool)>(new CommandDefinition(
            @"SELECT Barcode, ColourCode, WeightKg, CAST(CASE WHEN CbwtfHandoverUtc IS NULL THEN 0 ELSE 1 END AS BIT) AS HandedOver
              FROM dbo.WasteBag WHERE BranchId = @branchId ORDER BY BagId DESC", new { branchId }, cancellationToken: ct))).ToList();
    }
    public async Task<IReadOnlyList<(string, int, decimal)>> GetFormIvAsync(int branchId, CancellationToken ct = default)
    {
        using var c = await _f.CreateOpenConnectionAsync(ct);
        return (await c.QueryAsync<(string, int, decimal)>(new CommandDefinition(
            @"SELECT ColourCode, COUNT(1) AS Bags, ISNULL(SUM(WeightKg),0) AS Weight
              FROM dbo.WasteBag WHERE BranchId = @branchId GROUP BY ColourCode ORDER BY ColourCode", new { branchId }, cancellationToken: ct))).ToList();
    }
    public async Task<long> InsertMortuaryAsync(MortuaryRecord m, CancellationToken ct = default)
    {
        using var c = await _f.CreateOpenConnectionAsync(ct);
        const string sql = @"INSERT INTO dbo.MortuaryRecord (BranchId, PatientId, StorageNo, AdmittedUtc, PoliceIntimated, MlcLinked) VALUES (@BranchId, @PatientId, @StorageNo, @AdmittedUtc, @PoliceIntimated, @MlcLinked); SELECT CAST(SCOPE_IDENTITY() AS BIGINT);";
        return await c.QuerySingleAsync<long>(new CommandDefinition(sql, m, cancellationToken: ct));
    }
    public async Task ReleaseMortuaryAsync(long recordId, CancellationToken ct = default)
    {
        using var c = await _f.CreateOpenConnectionAsync(ct);
        await c.ExecuteAsync(new CommandDefinition("UPDATE dbo.MortuaryRecord SET ReleasedUtc = SYSUTCDATETIME() WHERE RecordId = @recordId", new { recordId }, cancellationToken: ct));
    }
    public async Task<IReadOnlyList<(long, string?, string?, string, string?, bool)>> GetMortuaryAsync(int branchId, CancellationToken ct = default)
    {
        using var c = await _f.CreateOpenConnectionAsync(ct);
        return (await c.QueryAsync<(long, string?, string?, string, string?, bool)>(new CommandDefinition(
            @"SELECT m.RecordId, p.FullName AS Patient, m.StorageNo,
                     CONVERT(varchar(16), m.AdmittedUtc, 120) AS Admitted, CONVERT(varchar(16), m.ReleasedUtc, 120) AS Released, m.MlcLinked
              FROM dbo.MortuaryRecord m LEFT JOIN dbo.Patient p ON p.PatientId = m.PatientId
              WHERE m.BranchId = @branchId ORDER BY m.RecordId DESC", new { branchId }, cancellationToken: ct))).ToList();
    }
    public async Task<string> NextMlcNoAsync(int branchId, CancellationToken ct = default)
    {
        using var c = await _f.CreateOpenConnectionAsync(ct);
        return await c.QuerySingleAsync<string>(new CommandDefinition("EXEC dbo.usp_NextDocNo @BranchId=@branchId, @DocType='MLC', @Prefix='MLC'", new { branchId }, cancellationToken: ct));
    }
    public async Task<long> InsertMlcAsync(MlcCase m, CancellationToken ct = default)
    {
        using var c = await _f.CreateOpenConnectionAsync(ct);
        const string sql = @"INSERT INTO dbo.MlcCase (MlcNo, BranchId, PatientId, PoliceStation, PoliceAckRef, InjuryDetails, CreatedUtc) VALUES (@MlcNo, @BranchId, @PatientId, @PoliceStation, @PoliceAckRef, @InjuryDetails, @CreatedUtc); SELECT CAST(SCOPE_IDENTITY() AS BIGINT);";
        return await c.QuerySingleAsync<long>(new CommandDefinition(sql, m, cancellationToken: ct));
    }
    public async Task IntimatePoliceAsync(long mlcId, string ackRef, CancellationToken ct = default)
    {
        using var c = await _f.CreateOpenConnectionAsync(ct);
        await c.ExecuteAsync(new CommandDefinition("UPDATE dbo.MlcCase SET PoliceAckRef = @ackRef WHERE MlcId = @mlcId", new { mlcId, ackRef }, cancellationToken: ct));
    }
    public async Task<IReadOnlyList<(long, string, string?, string?, string?, string)>> GetMlcAsync(int branchId, CancellationToken ct = default)
    {
        using var c = await _f.CreateOpenConnectionAsync(ct);
        return (await c.QueryAsync<(long, string, string?, string?, string?, string)>(new CommandDefinition(
            @"SELECT m.MlcId, m.MlcNo, p.FullName AS Patient, m.PoliceStation, m.PoliceAckRef,
                     CONVERT(varchar(16), m.CreatedUtc, 120) AS Created
              FROM dbo.MlcCase m LEFT JOIN dbo.Patient p ON p.PatientId = m.PatientId
              WHERE m.BranchId = @branchId ORDER BY m.MlcId DESC", new { branchId }, cancellationToken: ct))).ToList();
    }
}

public sealed class ExperienceRepository : IExperienceRepository
{
    private readonly IDbConnectionFactory _f;
    public ExperienceRepository(IDbConnectionFactory f) => _f = f;

    public async Task<IReadOnlyList<(int, string, string, string)>> GetConsentTemplatesAsync(CancellationToken ct = default)
    {
        using var c = await _f.CreateOpenConnectionAsync(ct);
        return (await c.QueryAsync<(int, string, string, string)>(new CommandDefinition("SELECT TemplateId, Code, Title, LanguageCode FROM dbo.ConsentTemplate ORDER BY Title", cancellationToken: ct))).ToList();
    }
    public async Task<long> InsertConsentCaptureAsync(ConsentCapture cc, CancellationToken ct = default)
    {
        using var c = await _f.CreateOpenConnectionAsync(ct);
        const string sql = @"INSERT INTO dbo.ConsentCapture (TemplateId, PatientId, SignatureType, CapturedUtc) VALUES (@TemplateId, @PatientId, @SignatureType, @CapturedUtc); SELECT CAST(SCOPE_IDENTITY() AS BIGINT);";
        return await c.QuerySingleAsync<long>(new CommandDefinition(sql, cc, cancellationToken: ct));
    }
    public async Task<IReadOnlyList<(int, string, string)>> GetCertTemplatesAsync(CancellationToken ct = default)
    {
        using var c = await _f.CreateOpenConnectionAsync(ct);
        return (await c.QueryAsync<(int, string, string)>(new CommandDefinition("SELECT TemplateId, CertType, Title FROM dbo.CertificateTemplate ORDER BY CertType", cancellationToken: ct))).ToList();
    }
    public async Task<long> InsertCertificateAsync(IssuedCertificate cert, CancellationToken ct = default)
    {
        using var c = await _f.CreateOpenConnectionAsync(ct);
        const string sql = @"INSERT INTO dbo.IssuedCertificate (TemplateId, PatientId, IssuedUtc, Status) VALUES (@TemplateId, @PatientId, @IssuedUtc, @Status); SELECT CAST(SCOPE_IDENTITY() AS BIGINT);";
        return await c.QuerySingleAsync<long>(new CommandDefinition(sql, cert, cancellationToken: ct));
    }
    public async Task ApproveCertificateAsync(long certId, int doctorId, CancellationToken ct = default)
    {
        using var c = await _f.CreateOpenConnectionAsync(ct);
        await c.ExecuteAsync(new CommandDefinition(
            "UPDATE dbo.IssuedCertificate SET ApprovedByDoctorId = @doctorId, Status = 'Issued', PdfUrl = CONCAT('certs/', CertId, '.pdf') WHERE CertId = @certId",
            new { certId, doctorId }, cancellationToken: ct));
    }
    public async Task<IReadOnlyList<(long, string, string, string)>> GetCertificatesAsync(int branchId, CancellationToken ct = default)
    {
        using var c = await _f.CreateOpenConnectionAsync(ct);
        return (await c.QueryAsync<(long, string, string, string)>(new CommandDefinition(
            @"SELECT ic.CertId, t.CertType, ISNULL(p.FullName,'') AS Patient, ic.Status
              FROM dbo.IssuedCertificate ic INNER JOIN dbo.CertificateTemplate t ON t.TemplateId = ic.TemplateId
              INNER JOIN dbo.Patient p ON p.PatientId = ic.PatientId
              WHERE p.RegBranchId = @branchId ORDER BY ic.CertId DESC", new { branchId }, cancellationToken: ct))).ToList();
    }
    public async Task<long> InsertSurveyAsync(FeedbackSurvey s, CancellationToken ct = default)
    {
        using var c = await _f.CreateOpenConnectionAsync(ct);
        const string sql = @"INSERT INTO dbo.FeedbackSurvey (PatientId, Score, Comments, CreatedUtc) VALUES (@PatientId, @Score, @Comments, @CreatedUtc); SELECT CAST(SCOPE_IDENTITY() AS BIGINT);";
        return await c.QuerySingleAsync<long>(new CommandDefinition(sql, s, cancellationToken: ct));
    }
    public async Task<long> InsertGrievanceAsync(Grievance g, CancellationToken ct = default)
    {
        using var c = await _f.CreateOpenConnectionAsync(ct);
        const string sql = @"INSERT INTO dbo.Grievance (BranchId, PatientId, Category, SlaDueUtc, Status, CreatedUtc) VALUES (@BranchId, @PatientId, @Category, @SlaDueUtc, @Status, @CreatedUtc); SELECT CAST(SCOPE_IDENTITY() AS BIGINT);";
        return await c.QuerySingleAsync<long>(new CommandDefinition(sql, g, cancellationToken: ct));
    }
    public async Task ResolveGrievanceAsync(long grievanceId, int tatMinutes, CancellationToken ct = default)
    {
        using var c = await _f.CreateOpenConnectionAsync(ct);
        await c.ExecuteAsync(new CommandDefinition("UPDATE dbo.Grievance SET Status = 'Resolved', ResolutionTatMinutes = @tatMinutes WHERE GrievanceId = @grievanceId", new { grievanceId, tatMinutes }, cancellationToken: ct));
    }
    public async Task<IReadOnlyList<(long, string?, string, string)>> GetGrievancesAsync(int branchId, CancellationToken ct = default)
    {
        using var c = await _f.CreateOpenConnectionAsync(ct);
        return (await c.QueryAsync<(long, string?, string, string)>(new CommandDefinition(
            "SELECT GrievanceId, Category, Status, CONVERT(varchar(16), CreatedUtc, 120) AS Created FROM dbo.Grievance WHERE BranchId = @branchId ORDER BY GrievanceId DESC", new { branchId }, cancellationToken: ct))).ToList();
    }
    public async Task<IReadOnlyList<(int, string, string)>> GetCountersAsync(int branchId, CancellationToken ct = default)
    {
        using var c = await _f.CreateOpenConnectionAsync(ct);
        return (await c.QueryAsync<(int, string, string)>(new CommandDefinition("SELECT CounterId, Area, CounterName FROM dbo.QueueCounter WHERE BranchId = @branchId AND IsActive = 1 ORDER BY Area, CounterName", new { branchId }, cancellationToken: ct))).ToList();
    }
    public async Task<string> IssueTokenAsync(int counterId, long? patientId, CancellationToken ct = default)
    {
        using var c = await _f.CreateOpenConnectionAsync(ct);
        var seq = await c.ExecuteScalarAsync<int>(new CommandDefinition(
            "SELECT COUNT(1) + 1 FROM dbo.QueueToken WHERE CounterId = @counterId AND CAST(IssuedUtc AS DATE) = CAST(SYSUTCDATETIME() AS DATE)", new { counterId }, cancellationToken: ct));
        var tokenNo = $"T-{seq:D2}";
        await c.ExecuteAsync(new CommandDefinition(
            "INSERT INTO dbo.QueueToken (CounterId, TokenNo, PatientId, IssuedUtc, Status) VALUES (@counterId, @tokenNo, @patientId, SYSUTCDATETIME(), 'Waiting')",
            new { counterId, tokenNo, patientId }, cancellationToken: ct));
        return tokenNo;
    }
    public async Task<string?> CallNextAsync(int counterId, CancellationToken ct = default)
    {
        using var c = await _f.CreateOpenConnectionAsync(ct);
        return await c.QuerySingleOrDefaultAsync<string?>(new CommandDefinition(
            @";WITH n AS (SELECT TOP 1 * FROM dbo.QueueToken WHERE CounterId = @counterId AND Status = 'Waiting' ORDER BY TokenId)
              UPDATE n SET Status = 'Called' OUTPUT inserted.TokenNo;", new { counterId }, cancellationToken: ct));
    }
    public async Task<IReadOnlyList<(string, string, string, string)>> GetQueueAsync(int branchId, CancellationToken ct = default)
    {
        using var c = await _f.CreateOpenConnectionAsync(ct);
        return (await c.QueryAsync<(string, string, string, string)>(new CommandDefinition(
            @"SELECT qc.Area, qc.CounterName, qt.TokenNo, qt.Status
              FROM dbo.QueueToken qt INNER JOIN dbo.QueueCounter qc ON qc.CounterId = qt.CounterId
              WHERE qc.BranchId = @branchId AND qt.Status IN ('Waiting','Called') AND CAST(qt.IssuedUtc AS DATE) = CAST(SYSUTCDATETIME() AS DATE)
              ORDER BY qc.Area, qt.TokenId", new { branchId }, cancellationToken: ct))).ToList();
    }
}
