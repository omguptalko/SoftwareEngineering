using System.Data;

namespace HIS.Application.Abstractions;

/// <summary>
/// Opens connections to the resolved tenant's databases (L1.6, R5). The target DB
/// names come from <c>ITenantContext</c> (populated per request from the DB catalog);
/// the connection string is built from "Provisioning:BaseConnection" with the catalog
/// swapped — never hardcoded. Throws if the tenant/DB is not resolved.
/// </summary>
public interface ITenantConnectionFactory
{
    /// <summary>Open the tenant's master DB (longitudinal data — D3).</summary>
    Task<IDbConnection> OpenMasterAsync(CancellationToken ct = default);
    /// <summary>Open the tenant's current fiscal-year data DB (fiscal-scoped data — D3).</summary>
    Task<IDbConnection> OpenDataAsync(CancellationToken ct = default);
}

/// <summary>
/// Demonstrates tenant-routed data access (L1.6): patients live in the tenant's
/// master DB, bills in its current fiscal-year DB. Proves master-vs-data routing
/// and tenant isolation without touching the legacy single-DB repositories (D7).
/// </summary>
public interface ITenantScopedRepository
{
    Task<(long PatientId, string Uhid)> AddPatientAsync(string fullName, string? mobile, CancellationToken ct = default);
    Task<IReadOnlyList<(long PatientId, string Uhid, string FullName)>> GetPatientsAsync(CancellationToken ct = default);
    Task<(long BillId, string BillNo)> AddBillAsync(decimal gross, CancellationToken ct = default);
    Task<IReadOnlyList<(long BillId, string BillNo, decimal Gross, string Status)>> GetBillsAsync(CancellationToken ct = default);
}