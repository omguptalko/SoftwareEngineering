using System.Text.Json;
using HIS.Application.Abstractions;
using HIS.Domain.Entities;
using HIS.Shared.Context;
using MediatR;

namespace HIS.Application.Behaviors;

/// <summary>
/// Writes an immutable audit row for every auditable (write) request — SRS §8.1/§3.22.
/// Captures user, branch, action, entity and outcome. Applied once, everywhere.
/// </summary>
public sealed class AuditBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly IAuditWriter _audit;
    private readonly IBranchContext _ctx;

    public AuditBehavior(IAuditWriter audit, IBranchContext ctx)
    {
        _audit = audit;
        _ctx = ctx;
    }

    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken ct)
    {
        if (request is not IAuditable auditable)
            return await next();   // queries are not audited

        string? error = null;
        var ok = true;
        try
        {
            return await next();
        }
        catch (Exception ex)
        {
            ok = false;
            error = ex.Message;
            throw;
        }
        finally
        {
            await _audit.WriteAsync(new AuditEntry
            {
                OccurredAtUtc = DateTime.UtcNow,
                BranchId = _ctx.BranchId,
                UserId = _ctx.UserId,
                UserName = _ctx.UserName,
                Action = typeof(TRequest).Name,
                Entity = auditable.AuditEntity,
                EntityId = auditable.AuditEntityId,
                PayloadJson = SafeSerialize(request),
                Succeeded = ok,
                Error = error
            }, CancellationToken.None);
        }
    }

    private static string? SafeSerialize(object request)
    {
        try { return JsonSerializer.Serialize(request); }
        catch { return null; }
    }
}
