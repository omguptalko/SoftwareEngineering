using Dapper;
using HIS.Application.Abstractions;
using HIS.Domain.Entities;
using HIS.Shared.Context;

namespace HIS.Infrastructure.Persistence;

// L1.8.5 cutover: Ambulance + Consent/Cert templates are master; dispatches, diet, waste,
// mortuary, MLC, consent captures, certificates, feedback, queue are fiscal-scoped (FY DB).
// Display names/labels from master are resolved app-side (D8 two-step).
public sealed class AmbulanceRepository : IAmbulanceRepository
{
    private readonly ITenantConnectionFactory _f;
    public AmbulanceRepository(ITenantConnectionFactory f) => _f = f;

    public async Task<IReadOnlyList<(int, string, string)>> GetAmbulancesAsync(int branchId, CancellationToken ct = default)
    {
        using var c = await _f.OpenMasterAsync(ct);
        return (await c.QueryAsync<(int, string, string)>(new CommandDefinition(
            "SELECT AmbulanceId, VehicleNo, Status FROM master.Ambulance WHERE BranchId = @branchId ORDER BY VehicleNo", new { branchId }, cancellationToken: ct))).ToList();
    }
    public async Task<int?> GetFirstAvailableAsync(int branchId, CancellationToken ct = default)
    {
        using var c = await _f.OpenMasterAsync(ct);
        return await c.QuerySingleOrDefaultAsync<int?>(new CommandDefinition(
            "SELECT TOP 1 AmbulanceId FROM master.Ambulance WHERE BranchId = @branchId AND Status = 'Available' ORDER BY AmbulanceId", new { branchId }, cancellationToken: ct));
    }
    public async Task<int> InsertAmbulanceAsync(int branchId, string vehicleNo, CancellationToken ct = default)
    {
        using var c = await _f.OpenMasterAsync(ct);
        return await c.QuerySingleAsync<int>(new CommandDefinition(
            @"INSERT INTO master.Ambulance (BranchId, VehicleNo, Status) VALUES (@branchId, @vehicleNo, 'Available');
              SELECT CAST(SCOPE_IDENTITY() AS INT);", new { branchId, vehicleNo }, cancellationToken: ct));
    }
    public async Task<bool> VehicleExistsAsync(int branchId, string vehicleNo, CancellationToken ct = default)
    {
        using var c = await _f.OpenMasterAsync(ct);
        return await c.QuerySingleOrDefaultAsync<int?>(new CommandDefinition(
            "SELECT TOP 1 AmbulanceId FROM master.Ambulance WHERE BranchId = @branchId AND VehicleNo = @vehicleNo", new { branchId, vehicleNo }, cancellationToken: ct)) is not null;
    }
    public async Task<long> InsertDispatchAsync(AmbulanceDispatch d, CancellationToken ct = default)
    {
        using var c = await _f.OpenDataAsync(ct);
        const string sql = @"
INSERT INTO support.AmbulanceDispatch (AmbulanceId, CallLoggedUtc, PickupLat, PickupLng, LastLat, LastLng, Status)
VALUES (@AmbulanceId, @CallLoggedUtc, @PickupLat, @PickupLng, @LastLat, @LastLng, @Status);
SELECT CAST(SCOPE_IDENTITY() AS BIGINT);";
        return await c.QuerySingleAsync<long>(new CommandDefinition(sql, d, cancellationToken: ct));
    }
    public async Task SetAmbulanceStatusAsync(int ambulanceId, string status, CancellationToken ct = default)
    {
        using var c = await _f.OpenMasterAsync(ct);
        await c.ExecuteAsync(new CommandDefinition("UPDATE master.Ambulance SET Status = @status WHERE AmbulanceId = @ambulanceId", new { ambulanceId, status }, cancellationToken: ct));
    }
    public async Task ArriveAsync(long dispatchId, decimal? lat, decimal? lng, CancellationToken ct = default)
    {
        using var c = await _f.OpenDataAsync(ct);
        await c.ExecuteAsync(new CommandDefinition(
            "UPDATE support.AmbulanceDispatch SET ArrivedUtc = SYSUTCDATETIME(), LastLat = @lat, LastLng = @lng, Status = 'Arrived' WHERE DispatchId = @dispatchId",
            new { dispatchId, lat, lng }, cancellationToken: ct));
    }
    public async Task<int> GetDispatchAmbulanceAsync(long dispatchId, CancellationToken ct = default)
    {
        using var c = await _f.OpenDataAsync(ct);
        return await c.QuerySingleAsync<int>(new CommandDefinition("SELECT AmbulanceId FROM support.AmbulanceDispatch WHERE DispatchId = @dispatchId", new { dispatchId }, cancellationToken: ct));
    }
    public async Task<IReadOnlyList<(long, string, string, string?, string)>> GetDispatchesAsync(int branchId, CancellationToken ct = default)
    {
        // Ambulance (vehicle no + branch) is master; dispatches are FY → two-step.
        Dictionary<int, string> ambs;
        using (var m = await _f.OpenMasterAsync(ct))
            ambs = (await m.QueryAsync<(int AmbulanceId, string VehicleNo)>(new CommandDefinition(
                "SELECT AmbulanceId, VehicleNo FROM master.Ambulance WHERE BranchId = @branchId", new { branchId }, cancellationToken: ct)))
                .ToDictionary(a => a.AmbulanceId, a => a.VehicleNo);
        using var c = await _f.OpenDataAsync(ct);
        var disp = await c.QueryAsync<(long DispatchId, int AmbulanceId, string Logged, string? Arrived, string Status)>(new CommandDefinition(
            @"SELECT DispatchId, AmbulanceId, CONVERT(varchar(16), CallLoggedUtc, 120) AS Logged,
                     CONVERT(varchar(16), ArrivedUtc, 120) AS Arrived, Status
              FROM support.AmbulanceDispatch ORDER BY DispatchId DESC", cancellationToken: ct));
        return disp.Where(d => ambs.ContainsKey(d.AmbulanceId))
                   .Select(d => (d.DispatchId, ambs[d.AmbulanceId], d.Logged, d.Arrived, d.Status)).ToList();
    }
}

public sealed class StatutoryRepository : IStatutoryRepository
{
    private readonly ITenantConnectionFactory _f;
    private readonly ITenantContext _tenant;
    public StatutoryRepository(ITenantConnectionFactory f, ITenantContext tenant) { _f = f; _tenant = tenant; }

    public async Task<long> InsertDietAsync(DietOrder d, CancellationToken ct = default)
    {
        using var c = await _f.OpenDataAsync(ct);
        const string sql = @"INSERT INTO support.DietOrder (AdmissionId, DietType, OrderedUtc, Cost) VALUES (@AdmissionId, @DietType, @OrderedUtc, @Cost); SELECT CAST(SCOPE_IDENTITY() AS BIGINT);";
        return await c.QuerySingleAsync<long>(new CommandDefinition(sql, d, cancellationToken: ct));
    }
    public async Task<IReadOnlyList<(long, string, string, decimal?)>> GetDietAsync(int branchId, CancellationToken ct = default)
    {
        List<(long DietOrderId, long AdmissionId, string DietType, decimal? Cost)> diet;
        using (var c = await _f.OpenDataAsync(ct))
            diet = (await c.QueryAsync<(long, long, string, decimal?)>(new CommandDefinition(
                "SELECT DietOrderId, AdmissionId, DietType, Cost FROM support.DietOrder ORDER BY DietOrderId DESC", cancellationToken: ct))).ToList();
        var admIds = diet.Select(d => d.AdmissionId).Distinct().ToArray();
        var adm = new Dictionary<long, (int BranchId, string Patient)>();
        if (admIds.Length > 0)
            using (var m = await _f.OpenMasterAsync(ct))
                adm = (await m.QueryAsync<(long AdmissionId, int BranchId, string Patient)>(new CommandDefinition(
                    @"SELECT a.AdmissionId, a.BranchId, ISNULL(p.FullName,'') AS Patient
                      FROM clinical.Admission a LEFT JOIN patient.Patient p ON p.PatientId = a.PatientId
                      WHERE a.AdmissionId IN @admIds", new { admIds }, cancellationToken: ct)))
                    .ToDictionary(r => r.AdmissionId, r => (r.BranchId, r.Patient));
        return diet.Where(d => adm.TryGetValue(d.AdmissionId, out var a) && a.BranchId == branchId)
                   .Select(d => (d.DietOrderId, adm[d.AdmissionId].Patient, d.DietType, d.Cost)).ToList();
    }
    public async Task<long> InsertWasteBagAsync(WasteBag b, CancellationToken ct = default)
    {
        using var c = await _f.OpenDataAsync(ct);
        const string sql = @"INSERT INTO support.WasteBag (BranchId, Barcode, ColourCode, WeightKg, GeneratedUtc) VALUES (@BranchId, @Barcode, @ColourCode, @WeightKg, @GeneratedUtc); SELECT CAST(SCOPE_IDENTITY() AS BIGINT);";
        return await c.QuerySingleAsync<long>(new CommandDefinition(sql, b, cancellationToken: ct));
    }
    public async Task HandoverWasteBagAsync(long bagId, CancellationToken ct = default)
    {
        using var c = await _f.OpenDataAsync(ct);
        await c.ExecuteAsync(new CommandDefinition("UPDATE support.WasteBag SET CbwtfHandoverUtc = SYSUTCDATETIME() WHERE BagId = @bagId", new { bagId }, cancellationToken: ct));
    }
    public async Task<IReadOnlyList<(string, string, decimal?, bool)>> GetWasteBagsAsync(int branchId, CancellationToken ct = default)
    {
        using var c = await _f.OpenDataAsync(ct);
        return (await c.QueryAsync<(string, string, decimal?, bool)>(new CommandDefinition(
            @"SELECT Barcode, ColourCode, WeightKg, CAST(CASE WHEN CbwtfHandoverUtc IS NULL THEN 0 ELSE 1 END AS BIT) AS HandedOver
              FROM support.WasteBag WHERE BranchId = @branchId ORDER BY BagId DESC", new { branchId }, cancellationToken: ct))).ToList();
    }
    public async Task<IReadOnlyList<(string, int, decimal)>> GetFormIvAsync(int branchId, CancellationToken ct = default)
    {
        using var c = await _f.OpenDataAsync(ct);
        return (await c.QueryAsync<(string, int, decimal)>(new CommandDefinition(
            @"SELECT ColourCode, COUNT(1) AS Bags, ISNULL(SUM(WeightKg),0) AS Weight
              FROM support.WasteBag WHERE BranchId = @branchId GROUP BY ColourCode ORDER BY ColourCode", new { branchId }, cancellationToken: ct))).ToList();
    }
    public async Task<long> InsertMortuaryAsync(MortuaryRecord m, CancellationToken ct = default)
    {
        using var c = await _f.OpenDataAsync(ct);
        const string sql = @"INSERT INTO support.MortuaryRecord (BranchId, PatientId, StorageNo, AdmittedUtc, PoliceIntimated, MlcLinked) VALUES (@BranchId, @PatientId, @StorageNo, @AdmittedUtc, @PoliceIntimated, @MlcLinked); SELECT CAST(SCOPE_IDENTITY() AS BIGINT);";
        return await c.QuerySingleAsync<long>(new CommandDefinition(sql, m, cancellationToken: ct));
    }
    public async Task ReleaseMortuaryAsync(long recordId, CancellationToken ct = default)
    {
        using var c = await _f.OpenDataAsync(ct);
        await c.ExecuteAsync(new CommandDefinition("UPDATE support.MortuaryRecord SET ReleasedUtc = SYSUTCDATETIME() WHERE RecordId = @recordId", new { recordId }, cancellationToken: ct));
    }
    public async Task<IReadOnlyList<(long, string?, string?, string, string?, bool)>> GetMortuaryAsync(int branchId, CancellationToken ct = default)
    {
        List<(long RecordId, long? PatientId, string? StorageNo, string Admitted, string? Released, bool Mlc)> recs;
        using (var c = await _f.OpenDataAsync(ct))
            recs = (await c.QueryAsync<(long, long?, string?, string, string?, bool)>(new CommandDefinition(
                @"SELECT RecordId, PatientId, StorageNo, CONVERT(varchar(16), AdmittedUtc, 120) AS Admitted,
                         CONVERT(varchar(16), ReleasedUtc, 120) AS Released, MlcLinked
                  FROM support.MortuaryRecord WHERE BranchId = @branchId ORDER BY RecordId DESC", new { branchId }, cancellationToken: ct))).ToList();
        var names = await MasterLookup.PatientNamesAsync(_f, recs.Where(r => r.PatientId.HasValue).Select(r => r.PatientId!.Value), ct);
        return recs.Select(r => (r.RecordId, r.PatientId.HasValue ? names.GetValueOrDefault(r.PatientId.Value) : null, r.StorageNo, r.Admitted, r.Released, r.Mlc)).ToList();
    }
    public async Task<string> NextMlcNoAsync(int branchId, CancellationToken ct = default)
    {
        using var c = await _f.OpenDataAsync(ct);
        return await c.QuerySingleAsync<string>(new CommandDefinition(
            "EXEC [proc].usp_NextDocNo @BranchId=@branchId, @DocType='MLC', @Prefix='MLC', @FyCode=@fy",
            new { branchId, fy = _tenant.FiscalYearCode ?? "" }, cancellationToken: ct));
    }
    public async Task<long> InsertMlcAsync(MlcCase m, CancellationToken ct = default)
    {
        using var c = await _f.OpenDataAsync(ct);
        const string sql = @"INSERT INTO support.MlcCase (MlcNo, BranchId, PatientId, PoliceStation, PoliceAckRef, InjuryDetails, CreatedUtc) VALUES (@MlcNo, @BranchId, @PatientId, @PoliceStation, @PoliceAckRef, @InjuryDetails, @CreatedUtc); SELECT CAST(SCOPE_IDENTITY() AS BIGINT);";
        return await c.QuerySingleAsync<long>(new CommandDefinition(sql, m, cancellationToken: ct));
    }
    public async Task IntimatePoliceAsync(long mlcId, string ackRef, CancellationToken ct = default)
    {
        using var c = await _f.OpenDataAsync(ct);
        await c.ExecuteAsync(new CommandDefinition("UPDATE support.MlcCase SET PoliceAckRef = @ackRef WHERE MlcId = @mlcId", new { mlcId, ackRef }, cancellationToken: ct));
    }
    public async Task<IReadOnlyList<(long, string, string?, string?, string?, string)>> GetMlcAsync(int branchId, CancellationToken ct = default)
    {
        List<(long MlcId, string MlcNo, long? PatientId, string? PoliceStation, string? PoliceAck, string Created)> mlc;
        using (var c = await _f.OpenDataAsync(ct))
            mlc = (await c.QueryAsync<(long, string, long?, string?, string?, string)>(new CommandDefinition(
                @"SELECT MlcId, MlcNo, PatientId, PoliceStation, PoliceAckRef, CONVERT(varchar(16), CreatedUtc, 120) AS Created
                  FROM support.MlcCase WHERE BranchId = @branchId ORDER BY MlcId DESC", new { branchId }, cancellationToken: ct))).ToList();
        var names = await MasterLookup.PatientNamesAsync(_f, mlc.Where(r => r.PatientId.HasValue).Select(r => r.PatientId!.Value), ct);
        return mlc.Select(r => (r.MlcId, r.MlcNo, r.PatientId.HasValue ? names.GetValueOrDefault(r.PatientId.Value) : null, r.PoliceStation, r.PoliceAck, r.Created)).ToList();
    }
}

public sealed class ExperienceRepository : IExperienceRepository
{
    private readonly ITenantConnectionFactory _f;
    public ExperienceRepository(ITenantConnectionFactory f) => _f = f;

    public async Task<IReadOnlyList<(int, string, string, string)>> GetConsentTemplatesAsync(CancellationToken ct = default)
    {
        using var c = await _f.OpenMasterAsync(ct);
        return (await c.QueryAsync<(int, string, string, string)>(new CommandDefinition("SELECT TemplateId, Code, Title, LanguageCode FROM master.ConsentTemplate ORDER BY Title", cancellationToken: ct))).ToList();
    }
    public async Task<long> InsertConsentCaptureAsync(ConsentCapture cc, CancellationToken ct = default)
    {
        using var c = await _f.OpenDataAsync(ct);
        const string sql = @"INSERT INTO support.ConsentCapture (TemplateId, PatientId, SignatureType, CapturedUtc) VALUES (@TemplateId, @PatientId, @SignatureType, @CapturedUtc); SELECT CAST(SCOPE_IDENTITY() AS BIGINT);";
        return await c.QuerySingleAsync<long>(new CommandDefinition(sql, cc, cancellationToken: ct));
    }
    public async Task<IReadOnlyList<(long, string, string, string?, DateTime)>> GetConsentCapturesAsync(CancellationToken ct = default)
    {
        using var c = await _f.OpenDataAsync(ct);
        var rows = (await c.QueryAsync<(long ConsentId, int TemplateId, long PatientId, string? SignatureType, DateTime CapturedUtc)>(new CommandDefinition(
            "SELECT ConsentId, TemplateId, PatientId, SignatureType, CapturedUtc FROM support.ConsentCapture ORDER BY ConsentId DESC", cancellationToken: ct))).ToList();
        var pats = await MasterLookup.PatientNamesAsync(_f, rows.Select(r => r.PatientId), ct);
        using var mc = await _f.OpenMasterAsync(ct);
        var tpls = (await mc.QueryAsync<(int TemplateId, string Title)>(new CommandDefinition(
            "SELECT TemplateId, Title FROM master.ConsentTemplate", cancellationToken: ct))).ToDictionary(t => t.TemplateId, t => t.Title);
        return rows.Select(r => (r.ConsentId, pats.GetValueOrDefault(r.PatientId, ""),
            tpls.GetValueOrDefault(r.TemplateId, "Template #" + r.TemplateId), r.SignatureType, r.CapturedUtc)).ToList();
    }
    public async Task<IReadOnlyList<(int, string, string)>> GetCertTemplatesAsync(CancellationToken ct = default)
    {
        using var c = await _f.OpenMasterAsync(ct);
        return (await c.QueryAsync<(int, string, string)>(new CommandDefinition("SELECT TemplateId, CertType, Title FROM master.CertificateTemplate ORDER BY CertType", cancellationToken: ct))).ToList();
    }
    public async Task<long> InsertCertificateAsync(IssuedCertificate cert, CancellationToken ct = default)
    {
        using var c = await _f.OpenDataAsync(ct);
        const string sql = @"INSERT INTO support.IssuedCertificate (TemplateId, PatientId, IssuedUtc, Status) VALUES (@TemplateId, @PatientId, @IssuedUtc, @Status); SELECT CAST(SCOPE_IDENTITY() AS BIGINT);";
        return await c.QuerySingleAsync<long>(new CommandDefinition(sql, cert, cancellationToken: ct));
    }
    public async Task ApproveCertificateAsync(long certId, int doctorId, CancellationToken ct = default)
    {
        using var c = await _f.OpenDataAsync(ct);
        await c.ExecuteAsync(new CommandDefinition(
            "UPDATE support.IssuedCertificate SET ApprovedByDoctorId = @doctorId, Status = 'Approved', PdfUrl = CONCAT('certs/', CertId, '.pdf') WHERE CertId = @certId",
            new { certId, doctorId }, cancellationToken: ct));
    }
    public async Task<IReadOnlyList<(long, string, string, string)>> GetCertificatesAsync(int branchId, CancellationToken ct = default)
    {
        List<(long CertId, int TemplateId, long PatientId, string Status)> certs;
        using (var c = await _f.OpenDataAsync(ct))
            certs = (await c.QueryAsync<(long, int, long, string)>(new CommandDefinition(
                "SELECT CertId, TemplateId, PatientId, Status FROM support.IssuedCertificate ORDER BY CertId DESC", cancellationToken: ct))).ToList();
        var names = await MasterLookup.PatientNamesAsync(_f, certs.Select(c => c.PatientId), ct);
        var typeIds = certs.Select(c => c.TemplateId).Distinct().ToArray();
        var types = new Dictionary<int, string>();
        if (typeIds.Length > 0)
            using (var m = await _f.OpenMasterAsync(ct))
                types = (await m.QueryAsync<(int TemplateId, string CertType)>(new CommandDefinition(
                    "SELECT TemplateId, CertType FROM master.CertificateTemplate WHERE TemplateId IN @typeIds", new { typeIds }, cancellationToken: ct)))
                    .ToDictionary(r => r.TemplateId, r => r.CertType);
        return certs.Select(c => (c.CertId, types.GetValueOrDefault(c.TemplateId, ""), names.GetValueOrDefault(c.PatientId, ""), c.Status)).ToList();
    }
    public async Task<long> InsertSurveyAsync(FeedbackSurvey s, CancellationToken ct = default)
    {
        using var c = await _f.OpenDataAsync(ct);
        const string sql = @"INSERT INTO support.FeedbackSurvey (PatientId, Score, Comments, CreatedUtc) VALUES (@PatientId, @Score, @Comments, @CreatedUtc); SELECT CAST(SCOPE_IDENTITY() AS BIGINT);";
        return await c.QuerySingleAsync<long>(new CommandDefinition(sql, s, cancellationToken: ct));
    }
    public async Task<long> InsertGrievanceAsync(Grievance g, CancellationToken ct = default)
    {
        using var c = await _f.OpenDataAsync(ct);
        const string sql = @"INSERT INTO support.Grievance (BranchId, PatientId, Category, SlaDueUtc, Status, CreatedUtc) VALUES (@BranchId, @PatientId, @Category, @SlaDueUtc, @Status, @CreatedUtc); SELECT CAST(SCOPE_IDENTITY() AS BIGINT);";
        return await c.QuerySingleAsync<long>(new CommandDefinition(sql, g, cancellationToken: ct));
    }
    public async Task ResolveGrievanceAsync(long grievanceId, int tatMinutes, CancellationToken ct = default)
    {
        using var c = await _f.OpenDataAsync(ct);
        await c.ExecuteAsync(new CommandDefinition("UPDATE support.Grievance SET Status = 'Resolved', ResolutionTatMinutes = @tatMinutes WHERE GrievanceId = @grievanceId", new { grievanceId, tatMinutes }, cancellationToken: ct));
    }
    public async Task<IReadOnlyList<(long, string?, string, string)>> GetGrievancesAsync(int branchId, CancellationToken ct = default)
    {
        using var c = await _f.OpenDataAsync(ct);
        return (await c.QueryAsync<(long, string?, string, string)>(new CommandDefinition(
            "SELECT GrievanceId, Category, Status, CONVERT(varchar(16), CreatedUtc, 120) AS Created FROM support.Grievance WHERE BranchId = @branchId ORDER BY GrievanceId DESC", new { branchId }, cancellationToken: ct))).ToList();
    }
    public async Task<IReadOnlyList<(int, string, string)>> GetCountersAsync(int branchId, CancellationToken ct = default)
    {
        using var c = await _f.OpenDataAsync(ct);
        return (await c.QueryAsync<(int, string, string)>(new CommandDefinition("SELECT CounterId, Area, CounterName FROM support.QueueCounter WHERE BranchId = @branchId AND IsActive = 1 ORDER BY Area, CounterName", new { branchId }, cancellationToken: ct))).ToList();
    }
    public async Task<string> IssueTokenAsync(int counterId, long? patientId, CancellationToken ct = default)
    {
        using var c = await _f.OpenDataAsync(ct);
        var seq = await c.ExecuteScalarAsync<int>(new CommandDefinition(
            "SELECT COUNT(1) + 1 FROM support.QueueToken WHERE CounterId = @counterId AND CAST(IssuedUtc AS DATE) = CAST(SYSUTCDATETIME() AS DATE)", new { counterId }, cancellationToken: ct));
        var tokenNo = $"T-{seq:D2}";
        await c.ExecuteAsync(new CommandDefinition(
            "INSERT INTO support.QueueToken (CounterId, TokenNo, PatientId, IssuedUtc, Status) VALUES (@counterId, @tokenNo, @patientId, SYSUTCDATETIME(), 'Waiting')",
            new { counterId, tokenNo, patientId }, cancellationToken: ct));
        return tokenNo;
    }
    public async Task<string?> CallNextAsync(int counterId, CancellationToken ct = default)
    {
        using var c = await _f.OpenDataAsync(ct);
        // Only today's waiting tokens (matches the board's date filter) so stale tokens from
        // previous days don't get called ahead of today's queue.
        return await c.QuerySingleOrDefaultAsync<string?>(new CommandDefinition(
            @";WITH n AS (SELECT TOP 1 * FROM support.QueueToken
                          WHERE CounterId = @counterId AND Status = 'Waiting'
                                AND CAST(IssuedUtc AS DATE) = CAST(SYSUTCDATETIME() AS DATE)
                          ORDER BY TokenId)
              UPDATE n SET Status = 'Called' OUTPUT inserted.TokenNo;", new { counterId }, cancellationToken: ct));
    }
    public async Task<IReadOnlyList<(string, string, string, string)>> GetQueueAsync(int branchId, CancellationToken ct = default)
    {
        using var c = await _f.OpenDataAsync(ct);
        return (await c.QueryAsync<(string, string, string, string)>(new CommandDefinition(
            @"SELECT qc.Area, qc.CounterName, qt.TokenNo, qt.Status
              FROM support.QueueToken qt INNER JOIN support.QueueCounter qc ON qc.CounterId = qt.CounterId
              WHERE qc.BranchId = @branchId AND qt.Status IN ('Waiting','Called') AND CAST(qt.IssuedUtc AS DATE) = CAST(SYSUTCDATETIME() AS DATE)
              ORDER BY qc.Area, qt.TokenId", new { branchId }, cancellationToken: ct))).ToList();
    }
}
