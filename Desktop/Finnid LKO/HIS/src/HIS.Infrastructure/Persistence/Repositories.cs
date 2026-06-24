using Dapper;
using HIS.Application.Abstractions;
using HIS.Domain.Entities;

namespace HIS.Infrastructure.Persistence;

// All SQL below is parameterized (Dapper) — no string concatenation of user input
// (SRS §8.1 injection prevention). Reference/master data comes from DB, not code.

public sealed class ModuleRegistryRepository : IModuleRegistryRepository
{
    private readonly IDbConnectionFactory _f;
    public ModuleRegistryRepository(IDbConnectionFactory f) => _f = f;

    public async Task<IReadOnlyList<ModuleGroup>> GetGroupsAsync(CancellationToken ct = default)
    {
        using var c = await _f.CreateOpenConnectionAsync(ct);
        var rows = await c.QueryAsync<ModuleGroup>(new CommandDefinition(
            "SELECT GroupId, Label, Icon, SortOrder FROM dbo.ModuleGroup ORDER BY SortOrder", cancellationToken: ct));
        return rows.ToList();
    }

    public async Task<IReadOnlyList<Module>> GetModulesAsync(CancellationToken ct = default)
    {
        using var c = await _f.CreateOpenConnectionAsync(ct);
        var rows = await c.QueryAsync<Module>(new CommandDefinition(
            "SELECT ModuleId, GroupId, Icon, Label, Built, Badge, SortOrder, SrsRef FROM dbo.Module ORDER BY SortOrder", cancellationToken: ct));
        return rows.ToList();
    }
}

public sealed class LookupRepository : ILookupRepository
{
    // L1.8 cutover: F3 master lookups are now served from the resolved tenant's
    // master DB (master.* schema) via ITenantConnectionFactory — not the legacy
    // single dbo database. Reads only; no cross-DB write/FK risk.
    private readonly ITenantConnectionFactory _f;
    public LookupRepository(ITenantConnectionFactory f) => _f = f;

    private static string Like(string? q) => "%" + (q ?? "").Trim() + "%";

    public async Task<IReadOnlyList<Doctor>> GetDoctorsAsync(string? q, CancellationToken ct = default)
    {
        using var c = await _f.OpenMasterAsync(ct);
        return (await c.QueryAsync<Doctor>(new CommandDefinition(
            @"SELECT DoctorId, Code, Name, Department, IsActive FROM master.Doctor
              WHERE IsActive = 1 AND (@q = '%%' OR Code LIKE @q OR Name LIKE @q OR Department LIKE @q)
              ORDER BY Name", new { q = Like(q) }, cancellationToken: ct))).ToList();
    }

    public async Task<IReadOnlyList<Drug>> GetDrugsAsync(string? q, CancellationToken ct = default)
    {
        using var c = await _f.OpenMasterAsync(ct);
        return (await c.QueryAsync<Drug>(new CommandDefinition(
            @"SELECT DrugId, Code, Name, Form, StockQty, ReorderLevel, IsActive FROM master.Drug
              WHERE IsActive = 1 AND (@q = '%%' OR Code LIKE @q OR Name LIKE @q)
              ORDER BY Name", new { q = Like(q) }, cancellationToken: ct))).ToList();
    }

    public async Task<IReadOnlyList<Icd10Code>> GetIcd10Async(string? q, CancellationToken ct = default)
    {
        using var c = await _f.OpenMasterAsync(ct);
        return (await c.QueryAsync<Icd10Code>(new CommandDefinition(
            @"SELECT Code, Description FROM master.Icd10Code
              WHERE (@q = '%%' OR Code LIKE @q OR Description LIKE @q)
              ORDER BY Code", new { q = Like(q) }, cancellationToken: ct))).ToList();
    }

    public async Task<IReadOnlyList<Payer>> GetPayersAsync(string? q, CancellationToken ct = default)
    {
        using var c = await _f.OpenMasterAsync(ct);
        return (await c.QueryAsync<Payer>(new CommandDefinition(
            @"SELECT PayerId, Code, Name, PayerType, IsActive FROM master.Payer
              WHERE IsActive = 1 AND (@q = '%%' OR Code LIKE @q OR Name LIKE @q OR PayerType LIKE @q)
              ORDER BY Name", new { q = Like(q) }, cancellationToken: ct))).ToList();
    }

    public async Task<IReadOnlyList<HbpPackage>> GetPackagesAsync(string? q, CancellationToken ct = default)
    {
        using var c = await _f.OpenMasterAsync(ct);
        return (await c.QueryAsync<HbpPackage>(new CommandDefinition(
            @"SELECT PackageId, Code, Name, Specialty, Rate, IsActive FROM master.HbpPackage
              WHERE IsActive = 1 AND (@q = '%%' OR Code LIKE @q OR Name LIKE @q OR Specialty LIKE @q)
              ORDER BY Code", new { q = Like(q) }, cancellationToken: ct))).ToList();
    }

    public async Task<IReadOnlyList<(string Ward, string Bed, string Status)>> GetWardBedsAsync(int branchId, string? q, CancellationToken ct = default)
    {
        using var c = await _f.OpenMasterAsync(ct);
        var rows = await c.QueryAsync<(string, string, string)>(new CommandDefinition(
            @"SELECT w.Name AS Ward, b.BedNo AS Bed, b.Status
              FROM master.Bed b INNER JOIN master.Ward w ON w.WardId = b.WardId
              WHERE w.BranchId = @branchId AND (@q = '%%' OR w.Name LIKE @q OR b.BedNo LIKE @q)
              ORDER BY w.Name, b.BedNo", new { branchId, q = Like(q) }, cancellationToken: ct));
        return rows.ToList();
    }

    public async Task<IReadOnlyList<BloodGroup>> GetBloodGroupsAsync(CancellationToken ct = default)
    {
        using var c = await _f.OpenMasterAsync(ct);
        return (await c.QueryAsync<BloodGroup>(new CommandDefinition(
            "SELECT Code, SortOrder FROM master.BloodGroup ORDER BY SortOrder", cancellationToken: ct))).ToList();
    }

    public async Task<IReadOnlyList<Tariff>> GetTariffsAsync(int branchId, string? q, CancellationToken ct = default)
    {
        using var c = await _f.OpenMasterAsync(ct);
        return (await c.QueryAsync<Tariff>(new CommandDefinition(
            @"SELECT TariffId, BranchId, ServiceCode, ServiceName, Category, Rate, GstRatePct, IsActive FROM master.Tariff
              WHERE IsActive = 1 AND (BranchId = @branchId OR BranchId IS NULL)
                    AND (@q = '%%' OR ServiceCode LIKE @q OR ServiceName LIKE @q OR Category LIKE @q)
              ORDER BY ServiceName", new { branchId, q = Like(q) }, cancellationToken: ct))).ToList();
    }
}

public sealed class PatientRepository : IPatientRepository
{
    private readonly IDbConnectionFactory _f;
    public PatientRepository(IDbConnectionFactory f) => _f = f;

    public async Task<Patient?> GetByUhidAsync(string uhid, CancellationToken ct = default)
    {
        using var c = await _f.CreateOpenConnectionAsync(ct);
        return await c.QuerySingleOrDefaultAsync<Patient>(new CommandDefinition(
            "SELECT * FROM dbo.Patient WHERE Uhid = @uhid AND IsActive = 1", new { uhid }, cancellationToken: ct));
    }

    public async Task<IReadOnlyList<Patient>> SearchAsync(string? q, int branchId, int take, CancellationToken ct = default)
    {
        using var c = await _f.CreateOpenConnectionAsync(ct);
        var like = "%" + (q ?? "").Trim() + "%";
        return (await c.QueryAsync<Patient>(new CommandDefinition(
            @"SELECT TOP (@take) * FROM dbo.Patient
              WHERE IsActive = 1 AND (@all = 1 OR Uhid LIKE @like OR FullName LIKE @like OR Mobile LIKE @like)
              ORDER BY PatientId DESC",
            new { take, like, all = string.IsNullOrWhiteSpace(q) ? 1 : 0 }, cancellationToken: ct))).ToList();
    }

    public async Task<IReadOnlyList<PatientVisit>> GetVisitsAsync(long patientId, CancellationToken ct = default)
    {
        using var c = await _f.CreateOpenConnectionAsync(ct);
        return (await c.QueryAsync<PatientVisit>(new CommandDefinition(
            @"SELECT VisitId, PatientId, BranchId, VisitDate, VisitType, DoctorName, Diagnosis, PayerName
              FROM dbo.PatientVisit WHERE PatientId = @patientId ORDER BY VisitDate DESC",
            new { patientId }, cancellationToken: ct))).ToList();
    }

    public async Task<string> GetNextUhidAsync(int branchId, CancellationToken ct = default)
    {
        using var c = await _f.CreateOpenConnectionAsync(ct);
        // UHID format BR{branch}-{yyyy}-{6-digit seq}; sequence is per branch+year, from DB.
        return await c.QuerySingleAsync<string>(new CommandDefinition(
            "EXEC dbo.usp_NextUhid @BranchId = @branchId", new { branchId }, cancellationToken: ct));
    }

    public async Task<long> InsertAsync(Patient p, CancellationToken ct = default)
    {
        using var c = await _f.CreateOpenConnectionAsync(ct);
        const string sql = @"
INSERT INTO dbo.Patient
 (Uhid, RegBranchId, RegisteredAtUtc, FullName, GuardianName, AgeYears, DateOfBirth, Sex, BloodGroup,
  Mobile, Email, MaritalStatus, Category, Address, City, State, Pincode, Occupation, EmployerPayerCode,
  AadhaarMasked, AbhaNumber, AbhaAddress, IsActive)
VALUES
 (@Uhid, @RegBranchId, @RegisteredAtUtc, @FullName, @GuardianName, @AgeYears, @DateOfBirth, @Sex, @BloodGroup,
  @Mobile, @Email, @MaritalStatus, @Category, @Address, @City, @State, @Pincode, @Occupation, @EmployerPayerCode,
  @AadhaarMasked, @AbhaNumber, @AbhaAddress, @IsActive);
SELECT CAST(SCOPE_IDENTITY() AS BIGINT);";
        return await c.QuerySingleAsync<long>(new CommandDefinition(sql, p, cancellationToken: ct));
    }

    public async Task<bool> AadhaarExistsAsync(string aadhaarMasked, CancellationToken ct = default)
    {
        using var c = await _f.CreateOpenConnectionAsync(ct);
        return await c.ExecuteScalarAsync<int>(new CommandDefinition(
            "SELECT COUNT(1) FROM dbo.Patient WHERE AadhaarMasked = @aadhaarMasked AND IsActive = 1",
            new { aadhaarMasked }, cancellationToken: ct)) > 0;
    }
}

public sealed class DashboardRepository : IDashboardRepository
{
    private readonly IDbConnectionFactory _f;
    public DashboardRepository(IDbConnectionFactory f) => _f = f;

    public async Task<IReadOnlyList<(string, string, string)>> GetKpisAsync(int branchId, CancellationToken ct = default)
    {
        using var c = await _f.CreateOpenConnectionAsync(ct);
        var rows = await c.QueryAsync<(string, string, string)>(new CommandDefinition(
            @"SELECT [Value], Label, Trend FROM dbo.DashboardKpi WHERE BranchId = @branchId ORDER BY SortOrder",
            new { branchId }, cancellationToken: ct));
        return rows.ToList();
    }

    public async Task<IReadOnlyList<(string, int, decimal)>> GetServiceActivityAsync(int branchId, CancellationToken ct = default)
    {
        using var c = await _f.CreateOpenConnectionAsync(ct);
        var rows = await c.QueryAsync<(string, int, decimal)>(new CommandDefinition(
            @"SELECT Service, [Count], Revenue FROM dbo.ServiceActivityDaily WHERE BranchId = @branchId ORDER BY SortOrder",
            new { branchId }, cancellationToken: ct));
        return rows.ToList();
    }
}
