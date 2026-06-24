namespace HIS.Shared.Context;

/// <summary>
/// Per-request tenant routing (L1.6). Resolved from the request host (own domain),
/// the common-domain subdomain, or an explicit tenant hint, then enriched with the
/// tenant's provisioned database names from the control-plane DB catalog.
/// Drives <c>ITenantConnectionFactory</c> so each request reaches the correct
/// per-tenant master DB and current fiscal-year data DB (R4/R5).
/// </summary>
public interface ITenantContext
{
    int? TenantId { get; }
    string? TenantCode { get; }
    int? FiscalYearId { get; }
    string? FiscalYearCode { get; }
    string? MasterDb { get; }
    string? DataDb { get; }
    bool IsResolved { get; }
}

/// <summary>Mutable implementation populated by TenantResolutionMiddleware.</summary>
public sealed class TenantContext : ITenantContext
{
    public int? TenantId { get; set; }
    public string? TenantCode { get; set; }
    public int? FiscalYearId { get; set; }
    public string? FiscalYearCode { get; set; }
    public string? MasterDb { get; set; }
    public string? DataDb { get; set; }
    public bool IsResolved => TenantId.HasValue;
}
