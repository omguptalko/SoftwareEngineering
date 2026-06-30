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
