using System.Security.Cryptography;
using System.Text;
using HIS.Application.Abstractions;
using Microsoft.Extensions.Configuration;

namespace HIS.Infrastructure.Security;

/// <summary>
/// AES-256-GCM authenticated field encryption at rest (parent 0.7). The 32-byte key
/// is read (base64) from config "Security:DataProtection:Key" — supplied via
/// environment / Azure Key Vault in prod, never hardcoded. Stored format is
/// "enc:v1:" + base64(nonce[12] | tag[16] | ciphertext). With no key configured the
/// service is a no-op and Unprotect returns legacy plaintext unchanged (backward-safe).
/// </summary>
public sealed class AesGcmFieldProtector : IFieldProtector
{
    private const string Prefix = "enc:v1:";
    private const int NonceLen = 12, TagLen = 16;
    private readonly byte[]? _key;

    public AesGcmFieldProtector(IConfiguration config)
    {
        var b64 = config["Security:DataProtection:Key"];
        if (!string.IsNullOrWhiteSpace(b64))
        {
            try { var k = Convert.FromBase64String(b64); if (k.Length == 32) _key = k; }
            catch { /* invalid key → treated as not configured */ }
        }
    }

    public bool IsEnabled => _key is not null;

    public string Protect(string plaintext)
    {
        if (_key is null || string.IsNullOrEmpty(plaintext)) return plaintext;
        var nonce = RandomNumberGenerator.GetBytes(NonceLen);
        var pt = Encoding.UTF8.GetBytes(plaintext);
        var ct = new byte[pt.Length];
        var tag = new byte[TagLen];
        using (var aes = new AesGcm(_key, TagLen)) aes.Encrypt(nonce, pt, ct, tag);
        var blob = new byte[NonceLen + TagLen + ct.Length];
        Buffer.BlockCopy(nonce, 0, blob, 0, NonceLen);
        Buffer.BlockCopy(tag, 0, blob, NonceLen, TagLen);
        Buffer.BlockCopy(ct, 0, blob, NonceLen + TagLen, ct.Length);
        return Prefix + Convert.ToBase64String(blob);
    }

    public string Unprotect(string stored)
    {
        if (_key is null || string.IsNullOrEmpty(stored) || !stored.StartsWith(Prefix, StringComparison.Ordinal))
            return stored;   // legacy plaintext or encryption disabled
        var blob = Convert.FromBase64String(stored[Prefix.Length..]);
        var nonce = blob.AsSpan(0, NonceLen);
        var tag = blob.AsSpan(NonceLen, TagLen);
        var ct = blob.AsSpan(NonceLen + TagLen);
        var pt = new byte[ct.Length];
        using (var aes = new AesGcm(_key, TagLen)) aes.Decrypt(nonce, ct, tag, pt);
        return Encoding.UTF8.GetString(pt);
    }
}
