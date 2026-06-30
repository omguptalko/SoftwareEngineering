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

/// <summary>RFC 6238 TOTP for multi-factor auth (L1.2.5).</summary>
public interface ITotpService
{
    string GenerateSecret();
    string GetProvisioningUri(string issuer, string accountName, string secret);
    bool Verify(string secret, string code, int window = 1);
}

/// <summary>
/// Reversible field-level encryption at rest (AES-256-GCM) for sensitive values
/// (parent 0.7 — "AES-256 at rest"). The key comes from config / Key Vault
/// (Security:DataProtection:Key) — never hardcoded. When no key is configured the
/// methods are pass-through (so dev works), and Unprotect tolerates legacy plaintext.
/// </summary>
public interface IFieldProtector
{
    bool IsEnabled { get; }
    string Protect(string plaintext);
    string Unprotect(string stored);
}

/// <summary>Control-plane identity store (HIS_Platform.security.*).</summary>
public interface IPlatformUserRepository
{
    Task<PlatformUser?> GetByUserNameAsync(string userName, CancellationToken ct = default);
    Task<IReadOnlyList<string>> GetRoleCodesAsync(long userId, CancellationToken ct = default);
    Task<int?> GetRoleIdByCodeAsync(string code, CancellationToken ct = default);
    Task<long> InsertUserAsync(PlatformUser user, CancellationToken ct = default);
    Task AssignRoleAsync(long userId, int roleId, CancellationToken ct = default);
    /// <summary>Assignable tenant-scope roles (for the create-user form).</summary>
    Task<IReadOnlyList<(string Code, string Name)>> ListRolesAsync(CancellationToken ct = default);
    /// <summary>Users belonging to a tenant, with their comma-joined role codes.</summary>
    Task<IReadOnlyList<(string UserName, string DisplayName, string? Email, bool IsActive, string Roles)>> ListUsersByTenantAsync(int tenantId, CancellationToken ct = default);
    // Tenant-user lifecycle. These touch ONLY tenant-bound users (TenantId IS NOT NULL),
    // so platform users (superadmin/demo) cannot be modified via the tenant-user admin.
    Task<bool> UpdateUserProfileAsync(string userName, string displayName, string? email, CancellationToken ct = default);
    Task<bool> SetUserActiveAsync(string userName, bool isActive, CancellationToken ct = default);
    Task<bool> SetUserPasswordAsync(string userName, string hash, string salt, CancellationToken ct = default);
    /// <summary>Replace all of a user's roles with the single given role.</summary>
    Task ReplaceUserRoleAsync(long userId, int roleId, CancellationToken ct = default);
    /// <summary>True if any of the user's roles is flagged IsPrivileged (MFA policy, L1.2.5).</summary>
    Task<bool> HasPrivilegedRoleAsync(long userId, CancellationToken ct = default);
    /// <summary>Store the user's TOTP secret and enable MFA.</summary>
    Task SetMfaSecretAsync(long userId, string secret, CancellationToken ct = default);
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
    /// <summary>Grant a single page-action (view/create/edit/delete/…) to a role (RolePageAction, L1.3.3).</summary>
    Task<bool> AssignPageActionToRoleAsync(string roleCode, string pageCode, string actionCode, CancellationToken ct = default);
    /// <summary>All active modules + pages (superadmin view).</summary>
    Task<IReadOnlyList<(string ModuleCode, string ModuleLabel, string? Icon, int ModuleSort, string PageCode, string PageLabel, string? Route, int PageSort)>>
        GetFullMenuAsync(CancellationToken ct = default);
    /// <summary>
    /// Modules + pages a set of roles can access (via RoleModule or RolePage), further
    /// filtered to the resolved tenant's enabled modules for the given fiscal year when
    /// tenantId+fiscalYearId are supplied (platform.TenantModule entitlement, L1.3.5/R3).
    /// </summary>
    Task<IReadOnlyList<(string ModuleCode, string ModuleLabel, string? Icon, int ModuleSort, string PageCode, string PageLabel, string? Route, int PageSort)>>
        GetMenuForRolesAsync(IEnumerable<string> roleCodes, int? tenantId, int? fiscalYearId, CancellationToken ct = default);
}

/// <summary>Names of the databases created/ensured during provisioning.</summary>
public sealed record ProvisionedDb(string DbKind, string DbName);

/// <summary>
/// Automated multi-DB provisioning engine (L1.5, R4). Creates the per-tenant master
/// DB and per-fiscal-year data DBs and applies their schema templates — no human
/// intervention. Server/connection/template settings come from "Provisioning:*" config
/// (Decision D5). DB names are validated to a strict identifier pattern.
/// </summary>
public interface IProvisioningEngine
{
    /// <summary>Ensures {Tenant}_Master exists + schema applied. Returns its DB name.</summary>
    Task<ProvisionedDb> ProvisionMasterAsync(string tenantCode, CancellationToken ct = default);
    /// <summary>Ensures {Tenant}_FY{fyCode} exists + schema applied. Returns its DB name.</summary>
    Task<ProvisionedDb> ProvisionFiscalYearAsync(string tenantCode, string fyCode, CancellationToken ct = default);
    /// <summary>Drops a tenant database (single-user + drop). Surfaces errors (decommission, L1.7).</summary>
    Task DropDatabaseAsync(string dbName, CancellationToken ct = default);
}

/// <summary>Control-plane tenant/fiscal-year/domain/db-catalog operations (L1.7).</summary>
public interface ITenantAdminRepository
{
    Task<bool> TenantExistsAsync(string code, CancellationToken ct = default);
    Task<int> CreateTenantAsync(string code, string name, byte fyStartMonth, byte fyStartDay, CancellationToken ct = default);
    Task<int?> GetTenantIdByCodeAsync(string code, CancellationToken ct = default);
    Task<bool> FiscalYearExistsAsync(int tenantId, string fyCode, CancellationToken ct = default);
    Task<int> CreateFiscalYearAsync(int tenantId, string code, DateTime start, DateTime end, bool isCurrent, CancellationToken ct = default);
    Task ClearCurrentFiscalYearAsync(int tenantId, CancellationToken ct = default);
    /// <summary>The tenant's current (IsCurrent=1) fiscal-year id, or null if none.</summary>
    Task<int?> GetCurrentFiscalYearIdAsync(int tenantId, CancellationToken ct = default);
    Task AddDomainAsync(int tenantId, string host, bool isPrimary, bool isCommon, CancellationToken ct = default);
    Task RegisterDbAsync(int tenantId, int? fiscalYearId, string dbKind, string dbName, string? connectionRef, CancellationToken ct = default);
    Task EnableAllModulesAsync(int tenantId, int fiscalYearId, CancellationToken ct = default);
    Task CopyModuleEntitlementsAsync(int tenantId, int fromFiscalYearId, int toFiscalYearId, CancellationToken ct = default);
    /// <summary>Enable/disable a single module for a tenant's fiscal year (platform.TenantModule, L1.3.3/L1.4.4).</summary>
    Task<bool> SetTenantModuleAsync(string tenantCode, string fyCode, string moduleCode, bool enabled, CancellationToken ct = default);
    // Per-fiscal-year billing (L1.4.2): subscription + ledger scoped to a tenant fiscal year.
    Task CreateSubscriptionAsync(int tenantId, int fiscalYearId, string plan, DateTime start, DateTime end, CancellationToken ct = default);
    Task WriteBillingLedgerAsync(int tenantId, int fiscalYearId, string entryType, decimal amount, string currency, string? notes, CancellationToken ct = default);
    /// <summary>Net ledger balance (SUM of Amount) for a tenant fiscal year.</summary>
    Task<decimal> GetLedgerBalanceAsync(int tenantId, int fiscalYearId, CancellationToken ct = default);
    Task<IReadOnlyList<(int TenantId, string Code, string Name, string? FyCode, string DbKind, string DbName)>> GetTenantsAsync(CancellationToken ct = default);
    Task<(int TenantId, string Code)?> ResolveTenantByHostAsync(string host, CancellationToken ct = default);
    /// <summary>Distinct database names registered for a tenant (for decommission).</summary>
    Task<IReadOnlyList<string>> GetTenantDatabaseNamesAsync(int tenantId, CancellationToken ct = default);
    /// <summary>Delete all control-plane rows + users for a tenant (FK order). Audit is retained.</summary>
    Task DeleteTenantAsync(int tenantId, CancellationToken ct = default);
    /// <summary>Full routing for a host (own domain or registered common-domain alias).</summary>
    Task<TenantRouting?> GetRoutingByHostAsync(string host, CancellationToken ct = default);
    /// <summary>Full routing for a tenant code (common-domain subdomain / explicit hint).</summary>
    Task<TenantRouting?> GetRoutingByCodeAsync(string code, CancellationToken ct = default);
}

/// <summary>A tenant's resolved databases for the current fiscal year (L1.6).</summary>
public sealed record TenantRouting(
    int TenantId, string Code, string? MasterDb,
    int? CurrentFiscalYearId, string? CurrentFiscalYearCode, string? DataDb);
