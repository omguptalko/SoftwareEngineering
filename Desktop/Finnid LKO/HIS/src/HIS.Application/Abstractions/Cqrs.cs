using MediatR;

namespace HIS.Application.Abstractions;

/// <summary>Marker for write use-cases (CQRS command side). SRS §9 architecture.</summary>
public interface ICommand<TResponse> : IRequest<TResponse> { }

/// <summary>Marker for read use-cases (CQRS query side).</summary>
public interface IQuery<TResponse> : IRequest<TResponse> { }

/// <summary>
/// Opt-in marker: requests implementing this are audited by the AuditBehavior
/// (SRS §8.1 immutable audit trail). Applied to all commands.
/// </summary>
public interface IAuditable
{
    string AuditEntity { get; }
    string? AuditEntityId { get; }
}

/// <summary>
/// Opt-in marker: requests implementing this are gated by the AuthorizationBehavior
/// (L1.2.6 RBAC). The caller must be authenticated AND hold <see cref="RequiredPermission"/>
/// (a platform/security permission code), unless they are the platform superadmin.
/// </summary>
public interface IAuthorizable
{
    string RequiredPermission { get; }
}

/// <summary>
/// Opt-in marker: the caller must be authenticated, but no specific permission is
/// required (e.g. self-scoped reads like "my menu"). Enforced by AuthorizationBehavior.
/// </summary>
public interface IRequireAuthentication { }
