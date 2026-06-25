using HIS.Shared.Context;
using Microsoft.AspNetCore.Mvc;

namespace HIS.Api.Controllers;

/// <summary>
/// Exposes the tenant realm resolved for the current request host (L1.7.4) so the
/// (pre-auth) login page can brand itself per hospital. Anonymous + read-only.
/// </summary>
[ApiController]
[Route("api/realm")]
public sealed class RealmController : ControllerBase
{
    private readonly ITenantContext _tenant;
    public RealmController(ITenantContext tenant) => _tenant = tenant;

    [HttpGet]
    public RealmDto Get() => new(
        _tenant.IsResolved, _tenant.TenantCode, _tenant.FiscalYearCode);

    public sealed record RealmDto(bool Resolved, string? TenantCode, string? FiscalYearCode);
}
