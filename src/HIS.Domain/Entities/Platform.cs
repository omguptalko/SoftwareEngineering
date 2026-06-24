namespace HIS.Domain.Entities;

/// <summary>
/// Control-plane user (HIS_Platform.security.AppUser). A platform superadmin has
/// TenantId = null and IsSuperAdmin = 1 (Decision D6); tenant users carry TenantId.
/// Password is stored as a PBKDF2 hash + salt — never plaintext.
/// </summary>
public sealed class PlatformUser
{
    public long UserId { get; set; }
    public int? TenantId { get; set; }
    public string UserName { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string? Email { get; set; }
    public string PasswordHash { get; set; } = "";
    public string PasswordSalt { get; set; } = "";
    public bool IsSuperAdmin { get; set; }
    public bool MfaEnabled { get; set; }
    public bool IsActive { get; set; } = true;
}
