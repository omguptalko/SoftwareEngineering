using HIS.Application.Abstractions;
using HIS.Shared.Context;

namespace HIS.Api.Middleware;

/// <summary>
/// Resolves the per-request <see cref="ITenantContext"/> (L1.6, R5) and loads the
/// tenant's provisioned database names from the control-plane catalog. Resolution order:
///   1. explicit <c>X-Tenant</c> header (tenant code) — API clients / login-time selection (D4 fallback)
///   2. exact host match in <c>platform.TenantDomain</c> — own domain or registered common alias
///   3. left-most label of a host under the configured common domain — subdomain routing (D4 primary)
/// Unresolved requests continue normally (non-tenant endpoints like /api/auth, /api/platform).
/// </summary>
public sealed class TenantResolutionMiddleware
{
    private readonly RequestDelegate _next;
    public TenantResolutionMiddleware(RequestDelegate next) => _next = next;

    public async Task Invoke(HttpContext http, ITenantContext ctx, ITenantAdminRepository tenants, IConfiguration config)
    {
        var tc = (TenantContext)ctx;
        TenantRouting? routing = null;

        var headerTenant = http.Request.Headers["X-Tenant"].FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(headerTenant))
        {
            routing = await tenants.GetRoutingByCodeAsync(headerTenant.Trim(), http.RequestAborted);
        }
        else
        {
            var host = http.Request.Host.Host;
            routing = await tenants.GetRoutingByHostAsync(host, http.RequestAborted);

            if (routing is null)
            {
                var commonDomain = config["Tenancy:CommonDomain"];
                if (!string.IsNullOrWhiteSpace(commonDomain) &&
                    host.EndsWith("." + commonDomain, StringComparison.OrdinalIgnoreCase))
                {
                    var label = host[..^(commonDomain.Length + 1)];               // left-most subdomain label
                    var code = label.Contains('.') ? label[(label.LastIndexOf('.') + 1)..] : label;
                    if (!string.IsNullOrWhiteSpace(code))
                        routing = await tenants.GetRoutingByCodeAsync(code, http.RequestAborted);
                }
            }

            // Dev fallback: resolve a configured default tenant (e.g. for the localhost
            // wireframe) so cut-over endpoints work without a real domain. Config-driven.
            if (routing is null)
            {
                var devTenant = config["Tenancy:DevDefaultTenant"];
                if (!string.IsNullOrWhiteSpace(devTenant))
                    routing = await tenants.GetRoutingByCodeAsync(devTenant, http.RequestAborted);
            }
        }

        if (routing is not null)
        {
            tc.TenantId = routing.TenantId;
            tc.TenantCode = routing.Code;
            tc.MasterDb = routing.MasterDb;
            tc.FiscalYearId = routing.CurrentFiscalYearId;
            tc.FiscalYearCode = routing.CurrentFiscalYearCode;
            tc.DataDb = routing.DataDb;
        }

        await _next(http);
    }
}
