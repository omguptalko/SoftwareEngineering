using System.Data;
using HIS.Domain.Entities;

namespace HIS.Application.Abstractions;

/// <summary>
/// Opens connections to the control-plane database (HIS_Platform), resolved from
/// "ConnectionStrings:Platform" — never hardcoded. Separate from the tenant
/// data-plane factory (<see cref="IDbConnectionFactory"/>).
/// </summary>
public interface IPlatformConnectionFactory
{
    Task<IDbConnection> CreateOpenConnectionAsync(CancellationToken ct = default);
}

/// <summary>Hashes and verifies passwords (PBKDF2). No business value hardcoded.</summary>
public interface IPasswordHasher
{
    /// <summary>Returns (hash, salt) as base64 strings for a fresh password.</summary>
    (string Hash, string Salt) Hash(string password);
    bool Verify(string password, string hash, string salt);
}

/// <summary>Issues signed JWTs from config (issuer/audience/key/expiry).</summary>
public interface IJwtTokenIssuer
{
    (string Token, DateTime ExpiresUtc) Issue(PlatformUser user, IReadOnlyCollection<string> roles);
}

/// <summary>Control-plane identity store (HIS_Platform.security.*).</summary>
public interface IPlatformUserRepository
{
    Task<PlatformUser?> GetByUserNameAsync(string userName, CancellationToken ct = default);
    Task<IReadOnlyList<string>> GetRoleCodesAsync(long userId, CancellationToken ct = default);
    Task<int?> GetRoleIdByCodeAsync(string code, CancellationToken ct = default);
    Task<long> InsertUserAsync(PlatformUser user, CancellationToken ct = default);
    Task AssignRoleAsync(long userId, int roleId, CancellationToken ct = default);
    Task WritePlatformAuditAsync(long? actorUserId, string? actorUserName, int? tenantId,
        string action, string entity, string? entityId, bool succeeded, string? error, CancellationToken ct = default);
    Task<IReadOnlyList<(DateTime OccurredUtc, string? Actor, string Action, string Entity, string? EntityId, bool Succeeded)>>
        GetRecentAuditAsync(int take, CancellationToken ct = default);
}

/// <summary>
/// Resolves the effective permission codes for a set of role codes, from the
/// control-plane RolePermission grants (L1.2.6). Used by the AuthorizationBehavior.
/// </summary>
public interface IPermissionResolver
{
    Task<IReadOnlySet<string>> GetPermissionsAsync(IEnumerable<string> roleCodes, CancellationToken ct = default);
}

/// <summary>
/// Dynamic module/page registry + role assignments + effective menu (L1.3, R3).
/// Backed by HIS_Platform.security.AppModule/AppPage/PageAction/RoleModule/RolePage.
/// </summary>
public interface IModuleAdminRepository
{
    Task<int> CreateModuleAsync(string code, string label, string? icon, int sortOrder, CancellationToken ct = default);
    Task<int> CreatePageAsync(string moduleCode, string code, string label, string? route, int sortOrder, CancellationToken ct = default);
    Task<bool> AssignModuleToRoleAsync(string roleCode, string moduleCode, CancellationToken ct = default);
    Task<bool> AssignPageToRoleAsync(string roleCode, string pageCode, CancellationToken ct = default);
    /// <summary>All active modules + pages (superadmin view).</summary>
    Task<IReadOnlyList<(string ModuleCode, string ModuleLabel, string? Icon, int ModuleSort, string PageCode, string PageLabel, string? Route, int PageSort)>>
        GetFullMenuAsync(CancellationToken ct = default);
    /// <summary>Modules + pages a set of roles can access (via RoleModule or RolePage).</summary>
    Task<IReadOnlyList<(string ModuleCode, string ModuleLabel, string? Icon, int ModuleSort, string PageCode, string PageLabel, string? Route, int PageSort)>>
        GetMenuForRolesAsync(IEnumerable<string> roleCodes, CancellationToken ct = default);
}
