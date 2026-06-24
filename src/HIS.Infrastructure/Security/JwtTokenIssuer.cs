using System.IdentityModel.Tokens.Jwt;
using System.Text;
using HIS.Application.Abstractions;
using HIS.Domain.Entities;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using Claim = System.Security.Claims.Claim;

namespace HIS.Infrastructure.Security;

/// <summary>
/// Issues signed JWTs. All parameters (key/issuer/audience/expiry) come from
/// the "Jwt" config section — never hardcoded (SRS §8.1). Claims align with what
/// BranchContextMiddleware reads: uid, name, role (+ tenantId, superadmin).
/// </summary>
public sealed class JwtTokenIssuer : IJwtTokenIssuer
{
    private readonly IConfiguration _config;
    public JwtTokenIssuer(IConfiguration config) => _config = config;

    public (string Token, DateTime ExpiresUtc) Issue(PlatformUser user, IReadOnlyCollection<string> roles)
    {
        var jwt = _config.GetSection("Jwt");
        var key = jwt["SigningKey"] ?? throw new InvalidOperationException("Missing config 'Jwt:SigningKey'.");
        var expiryMinutes = jwt.GetValue("ExpiryMinutes", 60);
        var expiresUtc = DateTime.UtcNow.AddMinutes(expiryMinutes);

        var claims = new List<Claim>
        {
            new("uid", user.UserId.ToString()),
            new("name", user.DisplayName),
            new(JwtRegisteredClaimNames.UniqueName, user.UserName),
            new("superadmin", user.IsSuperAdmin ? "1" : "0"),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };
        if (user.TenantId is int tid) claims.Add(new Claim("tenantId", tid.ToString()));
        foreach (var r in roles) claims.Add(new Claim("role", r));

        var creds = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)), SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: jwt["Issuer"],
            audience: jwt["Audience"],
            claims: claims,
            notBefore: DateTime.UtcNow,
            expires: expiresUtc,
            signingCredentials: creds);

        return (new JwtSecurityTokenHandler().WriteToken(token), expiresUtc);
    }
}
