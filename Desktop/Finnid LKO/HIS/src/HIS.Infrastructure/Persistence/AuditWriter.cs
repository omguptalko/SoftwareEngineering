using Dapper;
using HIS.Application.Abstractions;
using HIS.Domain.Entities;
using HIS.Shared.Context;

namespace HIS.Infrastructure.Persistence;

/// <summary>
/// Appends immutable audit rows to the resolved tenant's master DB (audit.AuditEntry).
/// Insert-only (SRS §8.1). If no tenant is resolved for the request (e.g. control-plane
/// /auth or /platform calls, which keep their own audit in HIS_Platform), this is a no-op.
/// </summary>
public sealed class AuditWriter : IAuditWriter
{
    private readonly ITenantConnectionFactory _f;
    private readonly ITenantContext _tenant;
    public AuditWriter(ITenantConnectionFactory f, ITenantContext tenant) { _f = f; _tenant = tenant; }

    public async Task WriteAsync(AuditEntry e, CancellationToken ct = default)
    {
        if (!_tenant.IsResolved) return;   // no tenant context → skip tenant audit
        using var conn = await _f.OpenMasterAsync(ct);
        const string sql = @"
INSERT INTO audit.AuditEntry
    (OccurredAtUtc, BranchId, UserId, UserName, Action, Entity, EntityId, PayloadJson, Succeeded, Error)
VALUES
    (@OccurredAtUtc, @BranchId, @UserId, @UserName, @Action, @Entity, @EntityId, @PayloadJson, @Succeeded, @Error);";
        await conn.ExecuteAsync(new CommandDefinition(sql, e, cancellationToken: ct));
    }
}
