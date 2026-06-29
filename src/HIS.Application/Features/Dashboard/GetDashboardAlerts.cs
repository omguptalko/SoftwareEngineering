using HIS.Application.Abstractions;
using HIS.Shared.Context;

namespace HIS.Application.Features.Dashboard;

/// <summary>One actionable item for the dashboard "Alerts &amp; Tasks" panel.</summary>
/// <param name="Severity">danger | warn | info | ok - drives the icon/colour client-side.</param>
public sealed record AlertDto(string Severity, string Icon, string Title, string Detail);

/// <summary>
/// Admin dashboard alerts feed (SRS 3.20, Phase 12.1). Aggregates real operational
/// signals from the existing module repositories - low blood stock, low inventory,
/// pending insurance claims and equipment due for maintenance - rather than a
/// hardcoded placeholder. Thresholds are the per-row masters (safety threshold,
/// reorder level, next-maintenance date), nothing is hardcoded here.
/// </summary>
public sealed record GetDashboardAlertsQuery : IQuery<IReadOnlyList<AlertDto>>;

public sealed class GetDashboardAlertsHandler
    : MediatR.IRequestHandler<GetDashboardAlertsQuery, IReadOnlyList<AlertDto>>
{
    // Claim statuses that need no further action - everything else counts as "pending".
    private static readonly HashSet<string> TerminalClaimStatuses =
        new(StringComparer.OrdinalIgnoreCase) { "Settled", "Rejected", "Denied", "Closed" };

    private readonly IBloodBankRepository _blood;
    private readonly IInventoryRepository _inventory;
    private readonly IClaimsRepository _claims;
    private readonly IAssetRepository _assets;
    private readonly IBranchContext _ctx;

    public GetDashboardAlertsHandler(
        IBloodBankRepository blood,
        IInventoryRepository inventory,
        IClaimsRepository claims,
        IAssetRepository assets,
        IBranchContext ctx)
    {
        _blood = blood; _inventory = inventory; _claims = claims; _assets = assets; _ctx = ctx;
    }

    public async Task<IReadOnlyList<AlertDto>> Handle(GetDashboardAlertsQuery request, CancellationToken ct)
    {
        var branchId = _ctx.BranchId ?? 0;
        var today = DateTime.UtcNow.Date;
        var alerts = new List<AlertDto>();

        // 1) Blood bank - groups at/below their safety threshold (SRS 3.7).
        var stock = await _blood.GetStockAsync(branchId, ct);
        var lowBlood = stock.Where(s => s.Units <= s.SafetyThreshold).ToList();
        if (lowBlood.Count > 0)
        {
            var detail = string.Join(", ", lowBlood
                .OrderBy(s => s.Units)
                .Select(s => $"{s.BloodGroup} {s.Units}u/<={s.SafetyThreshold}u"));
            alerts.Add(new AlertDto("danger", "bi-droplet-half",
                $"Blood stock low - {lowBlood.Count} group{(lowBlood.Count == 1 ? "" : "s")}", detail));
        }

        // 2) Inventory - items at/below reorder level (SRS 3.11).
        var lowStock = await _inventory.GetLowStockAsync(ct);
        if (lowStock.Count > 0)
        {
            var detail = string.Join(", ", lowStock
                .OrderBy(i => i.Stock)
                .Take(5)
                .Select(i => $"{i.Name} {i.Stock}/<={i.ReorderLevel}"));
            if (lowStock.Count > 5) detail += $", +{lowStock.Count - 5} more";
            alerts.Add(new AlertDto("warn", "bi-box-seam",
                $"{lowStock.Count} item{(lowStock.Count == 1 ? "" : "s")} below reorder level", detail));
        }

        // 3) Insurance - claims still needing action (SRS 3.15).
        var statusCounts = await _claims.GetStatusCountsAsync(branchId, ct);
        var pending = statusCounts.Where(s => !TerminalClaimStatuses.Contains(s.Status)).ToList();
        var pendingTotal = pending.Sum(s => s.Count);
        if (pendingTotal > 0)
        {
            var detail = string.Join(", ", pending
                .OrderByDescending(s => s.Count)
                .Select(s => $"{s.Status} {s.Count}"));
            alerts.Add(new AlertDto("info", "bi-clipboard2-pulse",
                $"{pendingTotal} claim{(pendingTotal == 1 ? "" : "s")} pending action", detail));
        }

        // 4) Assets - maintenance overdue / AMC expiring within 30 days (SRS 3.19).
        var assets = await _assets.GetAssetsAsync(branchId, ct);
        var maintDue = assets.Where(a => a.NextMaintenance is { } d && d.Date <= today).ToList();
        if (maintDue.Count > 0)
        {
            var detail = string.Join(", ", maintDue.Take(5).Select(a => $"{a.AssetTag} {a.Name}"));
            if (maintDue.Count > 5) detail += $", +{maintDue.Count - 5} more";
            alerts.Add(new AlertDto("warn", "bi-tools",
                $"{maintDue.Count} asset{(maintDue.Count == 1 ? "" : "s")} due for maintenance", detail));
        }
        var amcSoon = assets.Where(a => a.AmcExpiry is { } e && e.Date >= today && e.Date <= today.AddDays(30)).ToList();
        if (amcSoon.Count > 0)
        {
            var detail = string.Join(", ", amcSoon.Take(5).Select(a => $"{a.AssetTag} ({a.AmcExpiry:dd-MMM})"));
            alerts.Add(new AlertDto("info", "bi-shield-exclamation",
                $"{amcSoon.Count} AMC contract{(amcSoon.Count == 1 ? "" : "s")} expiring soon", detail));
        }

        if (alerts.Count == 0)
            alerts.Add(new AlertDto("ok", "bi-check-circle", "All clear", "No active alerts for this branch."));

        return alerts;
    }
}
