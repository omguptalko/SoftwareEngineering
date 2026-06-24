using HIS.Application.Abstractions;

namespace HIS.Application.Features.Platform;

/// <summary>
/// Reads the control-plane audit trail (L1.2.6 demo of RBAC gating).
/// Requires the 'audit.read' permission — superadmin holds it; other roles do not.
/// </summary>
public sealed record GetPlatformAuditQuery(int Take = 50) : IQuery<IReadOnlyList<PlatformAuditRow>>, IAuthorizable
{
    public string RequiredPermission => "audit.read";
}

public sealed record PlatformAuditRow(DateTime OccurredUtc, string? Actor, string Action, string Entity, string? EntityId, bool Succeeded);

public sealed class GetPlatformAuditHandler : MediatR.IRequestHandler<GetPlatformAuditQuery, IReadOnlyList<PlatformAuditRow>>
{
    private readonly IPlatformUserRepository _repo;
    public GetPlatformAuditHandler(IPlatformUserRepository repo) => _repo = repo;

    public async Task<IReadOnlyList<PlatformAuditRow>> Handle(GetPlatformAuditQuery q, CancellationToken ct)
    {
        var take = q.Take is > 0 and <= 500 ? q.Take : 50;
        var rows = await _repo.GetRecentAuditAsync(take, ct);
        return rows.Select(r => new PlatformAuditRow(r.Item1, r.Item2, r.Item3, r.Item4, r.Item5, r.Item6)).ToList();
    }
}
