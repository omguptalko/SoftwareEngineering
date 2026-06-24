using System.Security.Cryptography;
using HIS.Application.Abstractions;
using Microsoft.Extensions.Configuration;

namespace HIS.Infrastructure.Security;

/// <summary>
/// PBKDF2 (SHA-256) password hasher. Iteration count / sizes come from config
/// ("Security:Pbkdf2:*") with safe defaults — nothing security-relevant hardcoded
/// as a business value. Verify is constant-time.
/// </summary>
public sealed class PasswordHasher : IPasswordHasher
{
    private readonly int _iterations;
    private readonly int _saltBytes;
    private readonly int _hashBytes;

    public PasswordHasher(IConfiguration config)
    {
        _iterations = config.GetValue("Security:Pbkdf2:Iterations", 100_000);
        _saltBytes  = config.GetValue("Security:Pbkdf2:SaltBytes", 16);
        _hashBytes  = config.GetValue("Security:Pbkdf2:HashBytes", 32);
    }

    public (string Hash, string Salt) Hash(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(_saltBytes);
        var hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, _iterations, HashAlgorithmName.SHA256, _hashBytes);
        return (Convert.ToBase64String(hash), Convert.ToBase64String(salt));
    }

    public bool Verify(string password, string hash, string salt)
    {
        if (string.IsNullOrEmpty(hash) || string.IsNullOrEmpty(salt)) return false;
        byte[] saltBytes, expected;
        try { saltBytes = Convert.FromBase64String(salt); expected = Convert.FromBase64String(hash); }
        catch (FormatException) { return false; }

        var actual = Rfc2898DeriveBytes.Pbkdf2(password, saltBytes, _iterations, HashAlgorithmName.SHA256, expected.Length);
        return CryptographicOperations.FixedTimeEquals(actual, expected);
    }
}
