using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HIS.Application.Abstractions;
using HIS.Shared.Context;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;

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

    /// <summary>
    /// Dev-only convenience for the login page's realm selector: the list of tenant codes
    /// so a developer on localhost can pick which hospital to sign in to. Only returned when
    /// a dev-default tenant is configured (i.e. non-production) — empty otherwise, so it can
    /// never enumerate tenants in production.
    /// </summary>
    [HttpGet("tenants")]
    public async Task<IReadOnlyList<string>> DevTenants(
        [FromServices] ITenantAdminRepository tenants,
        [FromServices] IConfiguration config,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(config["Tenancy:DevDefaultTenant"]))
            return System.Array.Empty<string>();
        var rows = await tenants.GetTenantsAsync(ct);
        return rows.Select(r => r.Code)
                   .Distinct(System.StringComparer.OrdinalIgnoreCase)
                   .OrderBy(c => c, System.StringComparer.OrdinalIgnoreCase)
                   .ToList();
    }

    public sealed record RealmDto(bool Resolved, string? TenantCode, string? FiscalYearCode);
}
