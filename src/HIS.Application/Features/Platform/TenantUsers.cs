using FluentValidation;
using HIS.Application.Abstractions;
using HIS.Domain.Entities;
using HIS.Shared.Context;

namespace HIS.Application.Features.Platform;

// ============================ Create a tenant login user (tenant.manage) ============================

public sealed record CreateTenantUserResult(long UserId, string UserName, string TenantCode, string RoleCode);

/// <summary>
/// Creates a hospital (tenant) login user bound to that tenant, with a role grant
/// (L1.2 / L1.7.4). Superadmin-driven (gated by 'tenant.manage'); the password is
/// PBKDF2-hashed, the user is realm-checked at login (can sign in only to its own
/// hospital). Closes the onboarding gap where tenant users were config-seeded only.
/// </summary>
public sealed record CreateTenantUserCommand(
    string TenantCode, string UserName, string Password, string DisplayName, string? Email, string RoleCode)
    : ICommand<CreateTenantUserResult>, IAuthorizable
{
    public string RequiredPermission => "tenant.manage";
}

public sealed class CreateTenantUserValidator : AbstractValidator<CreateTenantUserCommand>
{
    public CreateTenantUserValidator()
    {
        RuleFor(x => x.TenantCode).NotEmpty().MaximumLength(40);
        RuleFor(x => x.UserName).NotEmpty().MaximumLength(120)
            .Matches(@"^[A-Za-z0-9._@-]+$").WithMessage("User name may use letters, digits and . _ @ - only.");
        RuleFor(x => x.Password).NotEmpty().MinimumLength(8).WithMessage("Password must be at least 8 characters.");
        RuleFor(x => x.DisplayName).NotEmpty().MaximumLength(160);
        RuleFor(x => x.RoleCode).NotEmpty().MaximumLength(40);
        RuleFor(x => x.Email).MaximumLength(190).EmailAddress().When(x => !string.IsNullOrWhiteSpace(x.Email));
    }
}

public sealed class CreateTenantUserHandler : MediatR.IRequestHandler<CreateTenantUserCommand, CreateTenantUserResult>
{
    private readonly IPlatformUserRepository _users;
    private readonly ITenantAdminRepository _tenants;
    private readonly IPasswordHasher _hasher;
    private readonly IBranchContext _ctx;

    public CreateTenantUserHandler(IPlatformUserRepository users, ITenantAdminRepository tenants, IPasswordHasher hasher, IBranchContext ctx)
    { _users = users; _tenants = tenants; _hasher = hasher; _ctx = ctx; }

    public async Task<CreateTenantUserResult> Handle(CreateTenantUserCommand c, CancellationToken ct)
    {
        var tenantId = await _tenants.GetTenantIdByCodeAsync(c.TenantCode, ct)
            ?? throw new InvalidOperationException($"Unknown tenant '{c.TenantCode}'.");

        if (await _users.GetByUserNameAsync(c.UserName, ct) is not null)
            throw new InvalidOperationException($"User name '{c.UserName}' is already taken.");

        var roleId = await _users.GetRoleIdByCodeAsync(c.RoleCode, ct)
            ?? throw new InvalidOperationException($"Unknown role '{c.RoleCode}'.");

        var (hash, salt) = _hasher.Hash(c.Password);
        long userId;
        try
        {
            userId = await _users.InsertUserAsync(new PlatformUser
            {
                TenantId = tenantId,
                UserName = c.UserName,
                DisplayName = c.DisplayName,
                Email = string.IsNullOrWhiteSpace(c.Email) ? null : c.Email,
                PasswordHash = hash,
                PasswordSalt = salt,
                IsSuperAdmin = false,
                IsActive = true
            }, ct);
            await _users.AssignRoleAsync(userId, roleId, ct);
        }
        catch
        {
            await _users.WritePlatformAuditAsync(_ctx.UserId, _ctx.UserName, tenantId,
                "CreateTenantUser", "AppUser", c.UserName, false, "insert failed", ct);
            throw;
        }

        await _users.WritePlatformAuditAsync(_ctx.UserId, _ctx.UserName, tenantId,
            "CreateTenantUser", "AppUser", c.UserName, true, null, ct);
        return new CreateTenantUserResult(userId, c.UserName, c.TenantCode, c.RoleCode);
    }
}

// ============================ List assignable roles (tenant.manage) ============================

public sealed record RoleDto(string Code, string Name);

public sealed record GetRolesQuery : IQuery<IReadOnlyList<RoleDto>>, IAuthorizable
{
    public string RequiredPermission => "tenant.manage";
}

public sealed class GetRolesHandler : MediatR.IRequestHandler<GetRolesQuery, IReadOnlyList<RoleDto>>
{
    private readonly IPlatformUserRepository _users;
    public GetRolesHandler(IPlatformUserRepository users) => _users = users;
    public async Task<IReadOnlyList<RoleDto>> Handle(GetRolesQuery q, CancellationToken ct) =>
        (await _users.ListRolesAsync(ct)).Select(r => new RoleDto(r.Code, r.Name)).ToList();
}

// ============================ List a tenant's users (tenant.manage) ============================

public sealed record TenantUserRow(string UserName, string DisplayName, string? Email, bool IsActive, string Roles);

public sealed record GetTenantUsersQuery(string TenantCode) : IQuery<IReadOnlyList<TenantUserRow>>, IAuthorizable
{
    public string RequiredPermission => "tenant.manage";
}

public sealed class GetTenantUsersHandler : MediatR.IRequestHandler<GetTenantUsersQuery, IReadOnlyList<TenantUserRow>>
{
    private readonly IPlatformUserRepository _users;
    private readonly ITenantAdminRepository _tenants;
    public GetTenantUsersHandler(IPlatformUserRepository users, ITenantAdminRepository tenants) { _users = users; _tenants = tenants; }

    public async Task<IReadOnlyList<TenantUserRow>> Handle(GetTenantUsersQuery q, CancellationToken ct)
    {
        var tenantId = await _tenants.GetTenantIdByCodeAsync(q.TenantCode, ct)
            ?? throw new InvalidOperationException($"Unknown tenant '{q.TenantCode}'.");
        return (await _users.ListUsersByTenantAsync(tenantId, ct))
            .Select(u => new TenantUserRow(u.UserName, u.DisplayName, u.Email, u.IsActive, u.Roles)).ToList();
    }
}

// ============================ Tenant-user lifecycle (tenant.manage) ============================
// All three resolve the user first to confirm it is a TENANT user (TenantId set) — platform
// users (superadmin/demo) are never editable here — and audit the action.

internal static class TenantUserGuard
{
    public static async Task<HIS.Domain.Entities.PlatformUser> RequireTenantUserAsync(
        IPlatformUserRepository users, string userName, CancellationToken ct)
    {
        var u = await users.GetByUserNameAsync(userName, ct);
        if (u is null || u.TenantId is null)
            throw new InvalidOperationException($"'{userName}' is not a tenant user.");
        return u;
    }
}

/// <summary>Edit a tenant user's display name / email.</summary>
public sealed record UpdateTenantUserCommand(string UserName, string DisplayName, string? Email) : ICommand<bool>, IAuthorizable
{
    public string RequiredPermission => "tenant.manage";
}
public sealed class UpdateTenantUserValidator : AbstractValidator<UpdateTenantUserCommand>
{
    public UpdateTenantUserValidator()
    {
        RuleFor(x => x.UserName).NotEmpty();
        RuleFor(x => x.DisplayName).NotEmpty().MaximumLength(160);
        RuleFor(x => x.Email).MaximumLength(190).EmailAddress().When(x => !string.IsNullOrWhiteSpace(x.Email));
    }
}
public sealed class UpdateTenantUserHandler : MediatR.IRequestHandler<UpdateTenantUserCommand, bool>
{
    private readonly IPlatformUserRepository _users; private readonly IBranchContext _ctx;
    public UpdateTenantUserHandler(IPlatformUserRepository users, IBranchContext ctx) { _users = users; _ctx = ctx; }
    public async Task<bool> Handle(UpdateTenantUserCommand c, CancellationToken ct)
    {
        var u = await TenantUserGuard.RequireTenantUserAsync(_users, c.UserName, ct);
        await _users.UpdateUserProfileAsync(c.UserName, c.DisplayName, string.IsNullOrWhiteSpace(c.Email) ? null : c.Email, ct);
        await _users.WritePlatformAuditAsync(_ctx.UserId, _ctx.UserName, u.TenantId, "UpdateTenantUser", "AppUser", c.UserName, true, null, ct);
        return true;
    }
}

/// <summary>Activate / deactivate a tenant user (a deactivated user can no longer log in).</summary>
public sealed record SetTenantUserActiveCommand(string UserName, bool IsActive) : ICommand<bool>, IAuthorizable
{
    public string RequiredPermission => "tenant.manage";
}
public sealed class SetTenantUserActiveHandler : MediatR.IRequestHandler<SetTenantUserActiveCommand, bool>
{
    private readonly IPlatformUserRepository _users; private readonly IBranchContext _ctx;
    public SetTenantUserActiveHandler(IPlatformUserRepository users, IBranchContext ctx) { _users = users; _ctx = ctx; }
    public async Task<bool> Handle(SetTenantUserActiveCommand c, CancellationToken ct)
    {
        var u = await TenantUserGuard.RequireTenantUserAsync(_users, c.UserName, ct);
        await _users.SetUserActiveAsync(c.UserName, c.IsActive, ct);
        await _users.WritePlatformAuditAsync(_ctx.UserId, _ctx.UserName, u.TenantId,
            c.IsActive ? "ActivateTenantUser" : "DeactivateTenantUser", "AppUser", c.UserName, true, null, ct);
        return true;
    }
}

/// <summary>Reset a tenant user's password (PBKDF2 re-hash).</summary>
public sealed record ResetTenantUserPasswordCommand(string UserName, string NewPassword) : ICommand<bool>, IAuthorizable
{
    public string RequiredPermission => "tenant.manage";
}
public sealed class ResetTenantUserPasswordValidator : AbstractValidator<ResetTenantUserPasswordCommand>
{
    public ResetTenantUserPasswordValidator()
    {
        RuleFor(x => x.UserName).NotEmpty();
        RuleFor(x => x.NewPassword).NotEmpty().MinimumLength(8).WithMessage("Password must be at least 8 characters.");
    }
}
public sealed class ResetTenantUserPasswordHandler : MediatR.IRequestHandler<ResetTenantUserPasswordCommand, bool>
{
    private readonly IPlatformUserRepository _users; private readonly IPasswordHasher _hasher; private readonly IBranchContext _ctx;
    public ResetTenantUserPasswordHandler(IPlatformUserRepository users, IPasswordHasher hasher, IBranchContext ctx)
    { _users = users; _hasher = hasher; _ctx = ctx; }
    public async Task<bool> Handle(ResetTenantUserPasswordCommand c, CancellationToken ct)
    {
        var u = await TenantUserGuard.RequireTenantUserAsync(_users, c.UserName, ct);
        var (hash, salt) = _hasher.Hash(c.NewPassword);
        await _users.SetUserPasswordAsync(c.UserName, hash, salt, ct);
        await _users.WritePlatformAuditAsync(_ctx.UserId, _ctx.UserName, u.TenantId, "ResetTenantUserPassword", "AppUser", c.UserName, true, null, ct);
        return true;
    }
}

/// <summary>Change a tenant user's role (replaces all role grants with the given role).
/// Takes effect on the user's next login (their current token keeps its old roles).</summary>
public sealed record ChangeTenantUserRoleCommand(string UserName, string RoleCode) : ICommand<bool>, IAuthorizable
{
    public string RequiredPermission => "tenant.manage";
}
public sealed class ChangeTenantUserRoleValidator : AbstractValidator<ChangeTenantUserRoleCommand>
{
    public ChangeTenantUserRoleValidator()
    {
        RuleFor(x => x.UserName).NotEmpty();
        RuleFor(x => x.RoleCode).NotEmpty().MaximumLength(40);
    }
}
public sealed class ChangeTenantUserRoleHandler : MediatR.IRequestHandler<ChangeTenantUserRoleCommand, bool>
{
    private readonly IPlatformUserRepository _users; private readonly IBranchContext _ctx;
    public ChangeTenantUserRoleHandler(IPlatformUserRepository users, IBranchContext ctx) { _users = users; _ctx = ctx; }
    public async Task<bool> Handle(ChangeTenantUserRoleCommand c, CancellationToken ct)
    {
        var u = await TenantUserGuard.RequireTenantUserAsync(_users, c.UserName, ct);
        var roleId = await _users.GetRoleIdByCodeAsync(c.RoleCode, ct)
            ?? throw new InvalidOperationException($"Unknown role '{c.RoleCode}'.");
        await _users.ReplaceUserRoleAsync(u.UserId, roleId, ct);
        await _users.WritePlatformAuditAsync(_ctx.UserId, _ctx.UserName, u.TenantId,
            "ChangeTenantUserRole", "AppUser", $"{c.UserName} -> {c.RoleCode}", true, null, ct);
        return true;
    }
}
