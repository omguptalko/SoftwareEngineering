using HIS.Shared.Context;

namespace HIS.Api.Middleware;

/// <summary>
/// Resolves the per-request <see cref="IBranchContext"/> from the authenticated
/// principal's claims (SRS §3.21 / §8.1). For the wireframe/dev surface where the
/// caller is not yet authenticated, it falls back to a CONFIGURED default branch
/// ("Dev:DefaultBranchId"/"Dev:DefaultBranchCode") — never a hardcoded literal.
/// </summary>
public sealed class BranchContextMiddleware
{
    private readonly RequestDelegate _next;
    public BranchContextMiddleware(RequestDelegate next) => _next = next;

    public async Task Invoke(HttpContext http, IBranchContext ctx, IConfiguration config)
    {
        var bc = (BranchContext)ctx;
        var user = http.User;

        if (user?.Identity?.IsAuthenticated == true)
        {
            bc.UserId = long.TryParse(user.FindFirst("uid")?.Value, out var uid) ? uid : null;
            bc.UserName = user.FindFirst("name")?.Value;
            bc.BranchId = int.TryParse(user.FindFirst("branchId")?.Value, out var bid) ? bid : null;
            bc.BranchCode = user.FindFirst("branchCode")?.Value;
            bc.Roles = user.FindAll("role").Select(c => c.Value).ToArray();
            bc.IsSuperAdmin = user.FindFirst("superadmin")?.Value == "1";
        }

        // Dev/wireframe fallback — config-driven, so nothing is hardcoded.
        bc.BranchId ??= config.GetValue<int?>("Dev:DefaultBranchId");
        bc.BranchCode ??= config.GetValue<string>("Dev:DefaultBranchCode");

        await _next(http);
    }
}
