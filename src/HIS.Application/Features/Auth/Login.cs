using FluentValidation;
using HIS.Application.Abstractions;
using HIS.Shared.Context;
using Microsoft.Extensions.Configuration;

namespace HIS.Application.Features.Auth;

/// <summary>
/// Authenticates a control-plane user and mints a JWT (L1.2; closes parent
/// tracker 0.6 "token issuance"). Login attempts are recorded in the
/// control-plane audit (audit.PlatformAudit), not the tenant audit trail.
/// </summary>
public sealed record LoginCommand(string UserName, string Password, string? MfaCode = null) : ICommand<LoginResult>;

public sealed record LoginResult(
    string Token, DateTime ExpiresUtc, string DisplayName, bool IsSuperAdmin,
    int? TenantId, IReadOnlyCollection<string> Roles, bool MfaEnrollmentRequired = false);

public sealed class LoginValidator : AbstractValidator<LoginCommand>
{
    public LoginValidator()
    {
        RuleFor(x => x.UserName).NotEmpty();
        RuleFor(x => x.Password).NotEmpty();
    }
}

public sealed class LoginHandler : MediatR.IRequestHandler<LoginCommand, LoginResult>
{
    private readonly IPlatformUserRepository _users;
    private readonly IPasswordHasher _hasher;
    private readonly IJwtTokenIssuer _jwt;
    private readonly ITenantContext _tenant;
    private readonly ITotpService _totp;
    private readonly IConfiguration _config;

    public LoginHandler(IPlatformUserRepository users, IPasswordHasher hasher, IJwtTokenIssuer jwt,
        ITenantContext tenant, ITotpService totp, IConfiguration config)
    { _users = users; _hasher = hasher; _jwt = jwt; _tenant = tenant; _totp = totp; _config = config; }

    public async Task<LoginResult> Handle(LoginCommand c, CancellationToken ct)
    {
        var user = await _users.GetByUserNameAsync(c.UserName, ct);

        // Uniform failure for unknown user / bad password / inactive — no enumeration.
        if (user is null || !user.IsActive || !_hasher.Verify(c.Password, user.PasswordHash, user.PasswordSalt))
        {
            await _users.WritePlatformAuditAsync(user?.UserId, c.UserName, user?.TenantId,
                "Login", "AppUser", c.UserName, succeeded: false, error: "Invalid credentials", ct);
            throw new InvalidOperationException("Invalid username or password.");
        }

        // Tenant-scoped login (L1.7.4, R5): a tenant-bound user may authenticate ONLY within
        // their own tenant realm (the tenant resolved from host / common-domain subdomain /
        // X-Tenant). Platform users (TenantId == null: superadmin + control-plane demo) are
        // realm-agnostic. Failure is reported uniformly (no tenant/account enumeration), with
        // the real reason recorded in the control-plane audit.
        if (!user.IsSuperAdmin && user.TenantId is int userTenant && _tenant.TenantId != userTenant)
        {
            await _users.WritePlatformAuditAsync(user.UserId, user.UserName, user.TenantId,
                "Login", "AppUser", c.UserName, succeeded: false,
                error: $"Tenant mismatch: user tenant {userTenant} vs realm {(_tenant.TenantId?.ToString() ?? "none")}", ct);
            throw new InvalidOperationException("Invalid username or password.");
        }

        // Multi-factor auth (L1.2.5, parent 0.7). If the user has enrolled (MfaEnabled), a valid
        // TOTP code is mandatory. Privileged-role users who have NOT yet enrolled are allowed in
        // (so they're never locked out before enrolling) but flagged to enrol — unless policy
        // 'Security:RequireMfaForPrivileged' is off. All gated on a configured Jwt issuer for the realm.
        var requirePrivileged = _config.GetValue("Security:RequireMfaForPrivileged", true);
        var enrollmentRequired = false;
        if (user.MfaEnabled && !string.IsNullOrWhiteSpace(user.MfaSecret))
        {
            if (string.IsNullOrWhiteSpace(c.MfaCode) || !_totp.Verify(user.MfaSecret!, c.MfaCode!))
            {
                await _users.WritePlatformAuditAsync(user.UserId, user.UserName, user.TenantId,
                    "Login", "AppUser", c.UserName, succeeded: false, error: "MFA code missing/invalid", ct);
                throw new InvalidOperationException("A valid authenticator code is required.");
            }
        }
        else if (requirePrivileged && await _users.HasPrivilegedRoleAsync(user.UserId, ct))
        {
            enrollmentRequired = true;   // privileged but not enrolled → prompt enrolment post-login
        }

        var roles = await _users.GetRoleCodesAsync(user.UserId, ct);
        var (token, expires) = _jwt.Issue(user, roles);

        await _users.WritePlatformAuditAsync(user.UserId, user.UserName, user.TenantId,
            "Login", "AppUser", user.UserId.ToString(), succeeded: true,
            error: enrollmentRequired ? "MFA enrollment required" : null, ct);

        return new LoginResult(token, expires, user.DisplayName, user.IsSuperAdmin, user.TenantId, roles, enrollmentRequired);
    }
}
