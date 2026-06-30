using HIS.Application.Abstractions;
using Microsoft.Extensions.Configuration;

namespace HIS.Application.Features.Ai;

public sealed record ForecastRow(
    string Code, string Name, int Stock, int ReorderLevel,
    decimal AvgDailyUse, decimal DaysOfCover, int SuggestedOrderQty, string Urgency);

/// <summary>
/// AI Inventory Forecasting (SRS 4.4, Phase 11.4). A transparent reorder-point projection:
/// from each item's reorder level and the configured supplier lead time it infers the
/// implied average daily consumption, projects days-of-cover, and proposes an order quantity
/// to reach the target cover. Explainable baseline behind the AI seam - an Azure ML demand
/// model can replace this handler. Lead time / target cover are config-driven (Ai:Forecast:*).
/// </summary>
public sealed record GetInventoryForecastQuery : IQuery<IReadOnlyList<ForecastRow>>, IRequireAuthentication;

public sealed class GetInventoryForecastHandler
    : MediatR.IRequestHandler<GetInventoryForecastQuery, IReadOnlyList<ForecastRow>>
{
    private readonly IInventoryRepository _inventory;
    private readonly IConfiguration _config;

    public GetInventoryForecastHandler(IInventoryRepository inventory, IConfiguration config)
    {
        _inventory = inventory; _config = config;
    }

    public async Task<IReadOnlyList<ForecastRow>> Handle(GetInventoryForecastQuery request, CancellationToken ct)
    {
        var leadDays = Math.Max(1, _config.GetValue("Ai:Forecast:LeadTimeDays", 7));
        var coverTarget = Math.Max(leadDays, _config.GetValue("Ai:Forecast:CoverTargetDays", 30));

        var items = await _inventory.GetStockLevelsAsync(ct);
        var rows = new List<ForecastRow>();
        foreach (var (code, name, stock, reorder) in items)
        {
            // Reorder point ~ leadTime x avgDailyUse => avgDailyUse ~ reorder / leadTime.
            var avgDaily = Math.Round((decimal)Math.Max(1, reorder) / leadDays, 2);
            var daysOfCover = avgDaily > 0 ? Math.Round(stock / avgDaily, 1) : 999m;
            var suggested = (int)Math.Max(0, Math.Ceiling(coverTarget * avgDaily) - stock);
            var urgency = daysOfCover <= leadDays ? "Critical"
                        : daysOfCover <= coverTarget / 2m ? "High"
                        : "Monitor";
            rows.Add(new ForecastRow(code, name, stock, reorder, avgDaily, daysOfCover, suggested, urgency));
        }
        // Most urgent (lowest cover) first.
        return rows.OrderBy(r => r.DaysOfCover).ToList();
    }
}
