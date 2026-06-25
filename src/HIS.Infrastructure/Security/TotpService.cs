using System.Security.Cryptography;
using System.Text;
using HIS.Application.Abstractions;

namespace HIS.Infrastructure.Security;

/// <summary>
/// RFC 6238 time-based one-time passwords (TOTP) for MFA (L1.2.5). HMAC-SHA1,
/// 30-second step, 6 digits — compatible with Google Authenticator / Authy etc.
/// The shared secret is Base32 (RFC 4648). No values hardcoded; issuer comes from config.
/// </summary>
public sealed class TotpService : ITotpService
{
    private const int Digits = 6;
    private const int StepSeconds = 30;

    public string GenerateSecret()
    {
        var bytes = RandomNumberGenerator.GetBytes(20);   // 160-bit secret
        return Base32Encode(bytes);
    }

    public string GetProvisioningUri(string issuer, string accountName, string secret)
    {
        // otpauth://totp/{issuer}:{account}?secret=...&issuer=...&digits=6&period=30
        var label = Uri.EscapeDataString($"{issuer}:{accountName}");
        var iss = Uri.EscapeDataString(issuer);
        return $"otpauth://totp/{label}?secret={secret}&issuer={iss}&algorithm=SHA1&digits={Digits}&period={StepSeconds}";
    }

    public bool Verify(string secret, string code, int window = 1)
    {
        if (string.IsNullOrWhiteSpace(secret) || string.IsNullOrWhiteSpace(code)) return false;
        code = code.Trim();
        var counter = DateTimeOffset.UtcNow.ToUnixTimeSeconds() / StepSeconds;
        // Accept the current step ± window to tolerate clock drift.
        for (var i = -window; i <= window; i++)
            if (FixedEquals(Compute(secret, counter + i), code)) return true;
        return false;
    }

    private static string Compute(string base32Secret, long counter)
    {
        var key = Base32Decode(base32Secret);
        var msg = new byte[8];
        for (var i = 7; i >= 0; i--) { msg[i] = (byte)(counter & 0xFF); counter >>= 8; }
        using var hmac = new HMACSHA1(key);
        var hash = hmac.ComputeHash(msg);
        var offset = hash[^1] & 0x0F;
        var binary = ((hash[offset] & 0x7F) << 24) | ((hash[offset + 1] & 0xFF) << 16)
                   | ((hash[offset + 2] & 0xFF) << 8) | (hash[offset + 3] & 0xFF);
        var otp = binary % (int)Math.Pow(10, Digits);
        return otp.ToString().PadLeft(Digits, '0');
    }

    // Constant-time-ish comparison to avoid leaking timing on the OTP.
    private static bool FixedEquals(string a, string b)
    {
        if (a.Length != b.Length) return false;
        var diff = 0;
        for (var i = 0; i < a.Length; i++) diff |= a[i] ^ b[i];
        return diff == 0;
    }

    private const string Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";

    private static string Base32Encode(byte[] data)
    {
        var sb = new StringBuilder();
        int buffer = 0, bits = 0;
        foreach (var b in data)
        {
            buffer = (buffer << 8) | b; bits += 8;
            while (bits >= 5) { sb.Append(Alphabet[(buffer >> (bits - 5)) & 31]); bits -= 5; }
        }
        if (bits > 0) sb.Append(Alphabet[(buffer << (5 - bits)) & 31]);
        return sb.ToString();
    }

    private static byte[] Base32Decode(string s)
    {
        s = s.TrimEnd('=').ToUpperInvariant();
        int buffer = 0, bits = 0;
        var output = new List<byte>(s.Length * 5 / 8);
        foreach (var c in s)
        {
            var val = Alphabet.IndexOf(c);
            if (val < 0) continue;   // skip stray separators
            buffer = (buffer << 5) | val; bits += 5;
            if (bits >= 8) { output.Add((byte)((buffer >> (bits - 8)) & 0xFF)); bits -= 8; }
        }
        return output.ToArray();
    }
}
