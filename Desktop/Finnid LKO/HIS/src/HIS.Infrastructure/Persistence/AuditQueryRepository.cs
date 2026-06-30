using Dapper;
using HIS.Application.Abstractions;

namespace HIS.Infrastructure.Persistence;

/// <summary>
/// Reads recent rows from the resolved tenant's immutable audit trail
/// (audit.AuditEntry in {Tenant}_Master), newest first. Read-only — the trail is
/// append-only (SRS §8.1). Parameterized via Dapper.
/// </summary>
public sealed class AuditQueryRepository : IAuditQueryRepository
{
    private readonly ITenantConnectionFactory _f;
    public AuditQueryRepository(ITenantConnectionFactory f) => _f = f;

    public async Task<IReadOnlyList<(DateTime OccurredAtUtc, string? UserName, string Action, string Entity, string? EntityId, bool Succeeded)>>
        GetRecentAsync(int take, CancellationToken ct = default)
    {
        using var conn = await _f.OpenMasterAsync(ct);
        var sql = @"
SELECT TOP (@Take) OccurredAtUtc, UserName, Action, Entity, EntityId, Succeeded
FROM audit.AuditEntry
ORDER BY OccurredAtUtc DESC, AuditId DESC;";
        var rows = await conn.QueryAsync<(DateTime OccurredAtUtc, string? UserName, string Action, string Entity, string? EntityId, bool Succeeded)>(
            new CommandDefinition(sql, new { Take = take }, cancellationToken: ct));
        return rows.AsList();
    }
}
