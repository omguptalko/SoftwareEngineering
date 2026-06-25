using HIS.Application.Abstractions;
using HIS.Shared.Context;
using Microsoft.Extensions.Configuration;

namespace HIS.Application.Features.Auth;

/// <summary>
/// Enrols the current (authenticated) user in TOTP MFA (L1.2.5): generates a secret,
/// stores it (MfaEnabled = 1) and returns the otpauth:// URI to add to an authenticator
/// app. The next login then requires a code. Requires authentication only.
/// </summary>
public sealed record EnrollMfaCommand : ICommand<EnrollMfaResult>, IRequireAuthentication;

public sealed record EnrollMfaResult(string Secret, string OtpauthUri);

public sealed class EnrollMfaHandler : MediatR.IRequestHandler<EnrollMfaCommand, EnrollMfaResult>
{
    private readonly IPlatformUserRepository _users;
    private readonly ITotpService _totp;
    private readonly IBranchContext _ctx;
    private readonly IConfiguration _config;

    public EnrollMfaHandler(IPlatformUserRepository users, ITotpService totp, IBranchContext ctx, IConfiguration config)
    { _users = users; _totp = totp; _ctx = ctx; _config = config; }

    public async Task<EnrollMfaResult> Handle(EnrollMfaCommand c, CancellationToken ct)
    {
        if (_ctx.UserId is not long userId)
            throw new InvalidOperationException("No authenticated user to enrol.");

        var secret = _totp.GenerateSecret();
        await _users.SetMfaSecretAsync(userId, secret, ct);

        var issuer = _config["Jwt:Issuer"] ?? "Finnid HIS";
        var account = _ctx.UserName ?? userId.ToString();
        var uri = _totp.GetProvisioningUri(issuer, account, secret);

        await _users.WritePlatformAuditAsync(userId, _ctx.UserName, null,
            "EnrollMfa", "AppUser", userId.ToString(), succeeded: true, error: null, ct);

        return new EnrollMfaResult(secret, uri);
    }
}
