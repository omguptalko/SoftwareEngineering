using HIS.Application.Abstractions;

namespace HIS.Application.Features.Compliance;

public sealed record AuditTrailRow(DateTime OccurredUtc, string? User, string Action, string Entity, string? EntityId, bool Succeeded);

/// <summary>
/// Reads the resolved tenant's immutable audit trail (SRS 3.22, Phase 12.2) for the
/// Compliance &amp; Audit screen. Requires an authenticated caller; rows are sourced from
/// the tenant master DB (audit.AuditEntry) written by the audit pipeline behavior.
/// </summary>
public sealed record GetAuditTrailQuery(int Take = 100) : IQuery<IReadOnlyList<AuditTrailRow>>, IRequireAuthentication;

public sealed class GetAuditTrailHandler : MediatR.IRequestHandler<GetAuditTrailQuery, IReadOnlyList<AuditTrailRow>>
{
    private readonly IAuditQueryRepository _repo;
    public GetAuditTrailHandler(IAuditQueryRepository repo) => _repo = repo;

    public async Task<IReadOnlyList<AuditTrailRow>> Handle(GetAuditTrailQuery q, CancellationToken ct)
    {
        var take = q.Take is > 0 and <= 500 ? q.Take : 100;
        var rows = await _repo.GetRecentAsync(take, ct);
        return rows.Select(r => new AuditTrailRow(r.OccurredAtUtc, r.UserName, r.Action, r.Entity, r.EntityId, r.Succeeded)).ToList();
    }
}
