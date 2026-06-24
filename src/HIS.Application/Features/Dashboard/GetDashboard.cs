using HIS.Application.Abstractions;
using HIS.Shared.Context;

namespace HIS.Application.Features.Dashboard;

public sealed record KpiDto(string Value, string Label, string Trend);
public sealed record ServiceActivityDto(string Service, int Count, decimal Revenue);
public sealed record DashboardDto(IReadOnlyList<KpiDto> Kpis, IReadOnlyList<ServiceActivityDto> Activity, decimal TotalRevenue, int TotalCount);

/// <summary>Admin dashboard (SRS §3.20) — replaces the hardcoded KPI/table HTML in modules.js.</summary>
public sealed record GetDashboardQuery : IQuery<DashboardDto>;

public sealed class GetDashboardHandler : MediatR.IRequestHandler<GetDashboardQuery, DashboardDto>
{
    private readonly IDashboardRepository _repo;
    private readonly IBranchContext _ctx;

    public GetDashboardHandler(IDashboardRepository repo, IBranchContext ctx)
    {
        _repo = repo; _ctx = ctx;
    }

    public async Task<DashboardDto> Handle(GetDashboardQuery request, CancellationToken ct)
    {
        var branchId = _ctx.BranchId ?? 0;
        var kpis = await _repo.GetKpisAsync(branchId, ct);
        var activity = await _repo.GetServiceActivityAsync(branchId, ct);

        return new DashboardDto(
            kpis.Select(k => new KpiDto(k.Value, k.Label, k.Trend)).ToList(),
            activity.Select(a => new ServiceActivityDto(a.Service, a.Count, a.Revenue)).ToList(),
            activity.Sum(a => a.Revenue),
            activity.Sum(a => a.Count));
    }
}
