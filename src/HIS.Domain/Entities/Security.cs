namespace HIS.Domain.Entities;

/// <summary>RBAC role — the 14 SRS §2.2 roles live as data, never as code enums.</summary>
public sealed class Role
{
    public int RoleId { get; set; }
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public bool IsPrivileged { get; set; }   // drives MFA requirement (SRS §8.1)
}

public sealed class Permission
{
    public int PermissionId { get; set; }
    public string Code { get; set; } = "";   // e.g. patient.register
    public string Description { get; set; } = "";
}

public sealed class AppUser
{
    public long UserId { get; set; }
    public int BranchId { get; set; }
    public string UserName { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string PasswordHash { get; set; } = "";
    public string PasswordSalt { get; set; } = "";
    public bool MfaEnabled { get; set; }
    public bool IsActive { get; set; } = true;
}

/// <summary>Immutable audit record — SRS §8.1 / §3.22, written by the audit pipeline behavior.</summary>
public sealed class AuditEntry
{
    public long AuditId { get; set; }
    public DateTime OccurredAtUtc { get; set; }
    public int? BranchId { get; set; }
    public long? UserId { get; set; }
    public string? UserName { get; set; }
    public string Action { get; set; } = "";      // CQRS request type name
    public string Entity { get; set; } = "";
    public string? EntityId { get; set; }
    public string? PayloadJson { get; set; }
    public bool Succeeded { get; set; }
    public string? Error { get; set; }
}
