using Dapper;
using HIS.Application.Abstractions;
using HIS.Domain.Entities;

namespace HIS.Infrastructure.Persistence;

/// <summary>Appends immutable audit rows (SRS §8.1). Insert-only; never updated/deleted.</summary>
public sealed class AuditWriter : IAuditWriter
{
    private readonly IDbConnectionFactory _factory;
    public AuditWriter(IDbConnectionFactory factory) => _factory = factory;

    public async Task WriteAsync(AuditEntry e, CancellationToken ct = default)
    {
        using var conn = await _factory.CreateOpenConnectionAsync(ct);
        const string sql = @"
INSERT INTO dbo.AuditEntry
    (OccurredAtUtc, BranchId, UserId, UserName, Action, Entity, EntityId, PayloadJson, Succeeded, Error)
VALUES
    (@OccurredAtUtc, @BranchId, @UserId, @UserName, @Action, @Entity, @EntityId, @PayloadJson, @Succeeded, @Error);";
        await conn.ExecuteAsync(new CommandDefinition(sql, e, cancellationToken: ct));
    }
}
