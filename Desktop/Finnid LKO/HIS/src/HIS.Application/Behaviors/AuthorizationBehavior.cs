using System.Security.Authentication;
using HIS.Application.Abstractions;
using HIS.Shared.Context;
using MediatR;

namespace HIS.Application.Behaviors;

/// <summary>
/// RBAC gate (L1.2.6). For requests marked <see cref="IAuthorizable"/>: the caller
/// must be authenticated and hold the required permission, unless they are the
/// platform superadmin (Decision D6). Runs after validation, before logging/audit.
///   - not authenticated      → <see cref="AuthenticationException"/>  (mapped to 401)
///   - authenticated, no perm  → <see cref="UnauthorizedAccessException"/> (mapped to 403)
/// </summary>
public sealed class AuthorizationBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly IBranchContext _ctx;
    private readonly IPermissionResolver _permissions;

    public AuthorizationBehavior(IBranchContext ctx, IPermissionResolver permissions)
    { _ctx = ctx; _permissions = permissions; }

    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken ct)
    {
        if (request is IRequireAuthentication && !_ctx.IsAuthenticated)
            throw new AuthenticationException("Authentication required.");

        if (request is IAuthorizable auth)
        {
            if (!_ctx.IsAuthenticated)
                throw new AuthenticationException("Authentication required.");

            if (!_ctx.IsSuperAdmin)
            {
                var held = await _permissions.GetPermissionsAsync(_ctx.Roles, ct);
                if (!held.Contains(auth.RequiredPermission))
                    throw new UnauthorizedAccessException($"Missing permission '{auth.RequiredPermission}'.");
            }
        }
        return await next();
    }
}
