using FluentValidation;
using HIS.Application.Abstractions;

namespace HIS.Application.Features.Auth;

/// <summary>
/// Authenticates a control-plane user and mints a JWT (L1.2; closes parent
/// tracker 0.6 "token issuance"). Login attempts are recorded in the
/// control-plane audit (audit.PlatformAudit), not the tenant audit trail.
/// </summary>
public sealed record LoginCommand(string UserName, string Password) : ICommand<LoginResult>;

public sealed record LoginResult(
    string Token, DateTime ExpiresUtc, string DisplayName, bool IsSuperAdmin,
    int? TenantId, IReadOnlyCollection<string> Roles);

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

    public LoginHandler(IPlatformUserRepository users, IPasswordHasher hasher, IJwtTokenIssuer jwt)
    { _users = users; _hasher = hasher; _jwt = jwt; }

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

        var roles = await _users.GetRoleCodesAsync(user.UserId, ct);
        var (token, expires) = _jwt.Issue(user, roles);

        await _users.WritePlatformAuditAsync(user.UserId, user.UserName, user.TenantId,
            "Login", "AppUser", user.UserId.ToString(), succeeded: true, error: null, ct);

        return new LoginResult(token, expires, user.DisplayName, user.IsSuperAdmin, user.TenantId, roles);
    }
}
