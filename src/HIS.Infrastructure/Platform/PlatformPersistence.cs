using System.Data;
using Dapper;
using HIS.Application.Abstractions;
using HIS.Domain.Entities;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

namespace HIS.Infrastructure.Platform;

/// <summary>
/// Opens connections to the control-plane DB (HIS_Platform) from
/// "ConnectionStrings:Platform". Distinct from the tenant data-plane factory.
/// </summary>
public sealed class PlatformConnectionFactory : IPlatformConnectionFactory
{
    private readonly string _connectionString;

    public PlatformConnectionFactory(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("Platform")
            ?? throw new InvalidOperationException("Missing connection string 'ConnectionStrings:Platform'.");
    }

    public async Task<IDbConnection> CreateOpenConnectionAsync(CancellationToken ct = default)
    {
        var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync(ct);
        return conn;
    }
}

/// <summary>Control-plane identity store over HIS_Platform.security.* (parameterized Dapper).</summary>
public sealed class PlatformUserRepository : IPlatformUserRepository
{
    private readonly IPlatformConnectionFactory _f;
    private readonly IFieldProtector _protector;
    public PlatformUserRepository(IPlatformConnectionFactory f, IFieldProtector protector) { _f = f; _protector = protector; }

    public async Task<PlatformUser?> GetByUserNameAsync(string userName, CancellationToken ct = default)
    {
        using var c = await _f.CreateOpenConnectionAsync(ct);
        var user = await c.QuerySingleOrDefaultAsync<PlatformUser>(new CommandDefinition(
            @"SELECT UserId, TenantId, UserName, DisplayName, Email, PasswordHash, PasswordSalt,
                     IsSuperAdmin, MfaEnabled, MfaSecret, IsActive
              FROM security.AppUser WHERE UserName = @userName",
            new { userName }, cancellationToken: ct));
        // Decrypt the MFA secret at read (AES-at-rest, parent 0.7). Tolerates legacy plaintext.
        if (user?.MfaSecret is not null) user.MfaSecret = _protector.Unprotect(user.MfaSecret);
        return user;
    }

    public async Task<IReadOnlyList<string>> GetRoleCodesAsync(long userId, CancellationToken ct = default)
    {
        using var c = await _f.CreateOpenConnectionAsync(ct);
        return (await c.QueryAsync<string>(new CommandDefinition(
            @"SELECT r.Code FROM security.UserRole ur
              INNER JOIN security.Role r ON r.RoleId = ur.RoleId
              WHERE ur.UserId = @userId ORDER BY r.Code",
            new { userId }, cancellationToken: ct))).ToList();
    }

    public async Task<int?> GetRoleIdByCodeAsync(string code, CancellationToken ct = default)
    {
        using var c = await _f.CreateOpenConnectionAsync(ct);
        return await c.QuerySingleOrDefaultAsync<int?>(new CommandDefinition(
            "SELECT RoleId FROM security.Role WHERE Code = @code", new { code }, cancellationToken: ct));
    }

    public async Task<long> InsertUserAsync(PlatformUser u, CancellationToken ct = default)
    {
        using var c = await _f.CreateOpenConnectionAsync(ct);
        const string sql = @"
INSERT INTO security.AppUser (TenantId, UserName, DisplayName, Email, PasswordHash, PasswordSalt, IsSuperAdmin, MfaEnabled, IsActive)
VALUES (@TenantId, @UserName, @DisplayName, @Email, @PasswordHash, @PasswordSalt, @IsSuperAdmin, @MfaEnabled, @IsActive);
SELECT CAST(SCOPE_IDENTITY() AS BIGINT);";
        return await c.QuerySingleAsync<long>(new CommandDefinition(sql, u, cancellationToken: ct));
    }

    public async Task AssignRoleAsync(long userId, int roleId, CancellationToken ct = default)
    {
        using var c = await _f.CreateOpenConnectionAsync(ct);
        await c.ExecuteAsync(new CommandDefinition(
            @"IF NOT EXISTS (SELECT 1 FROM security.UserRole WHERE UserId = @userId AND RoleId = @roleId)
                  INSERT security.UserRole (UserId, RoleId) VALUES (@userId, @roleId);",
            new { userId, roleId }, cancellationToken: ct));
    }

    public async Task<IReadOnlyList<(string Code, string Name)>> ListRolesAsync(CancellationToken ct = default)
    {
        using var c = await _f.CreateOpenConnectionAsync(ct);
        return (await c.QueryAsync<(string Code, string Name)>(new CommandDefinition(
            "SELECT Code, Name FROM security.Role WHERE Scope = 'tenant' ORDER BY Name", cancellationToken: ct))).ToList();
    }

    public async Task<IReadOnlyList<(string UserName, string DisplayName, string? Email, bool IsActive, string Roles)>>
        ListUsersByTenantAsync(int tenantId, CancellationToken ct = default)
    {
        using var c = await _f.CreateOpenConnectionAsync(ct);
        return (await c.QueryAsync<(string UserName, string DisplayName, string? Email, bool IsActive, string Roles)>(
            new CommandDefinition(
            @"SELECT u.UserName, u.DisplayName, u.Email, u.IsActive,
                     ISNULL(STRING_AGG(r.Code, ', '), '') AS Roles
              FROM security.AppUser u
              LEFT JOIN security.UserRole ur ON ur.UserId = u.UserId
              LEFT JOIN security.Role r ON r.RoleId = ur.RoleId
              WHERE u.TenantId = @tenantId
              GROUP BY u.UserName, u.DisplayName, u.Email, u.IsActive
              ORDER BY u.UserName",
            new { tenantId }, cancellationToken: ct))).ToList();
    }

    public async Task<bool> UpdateUserProfileAsync(string userName, string displayName, string? email, CancellationToken ct = default)
    {
        using var c = await _f.CreateOpenConnectionAsync(ct);
        return await c.ExecuteAsync(new CommandDefinition(
            @"UPDATE security.AppUser SET DisplayName = @displayName, Email = @email
              WHERE UserName = @userName AND TenantId IS NOT NULL",
            new { userName, displayName, email }, cancellationToken: ct)) > 0;
    }

    public async Task<bool> SetUserActiveAsync(string userName, bool isActive, CancellationToken ct = default)
    {
        using var c = await _f.CreateOpenConnectionAsync(ct);
        return await c.ExecuteAsync(new CommandDefinition(
            "UPDATE security.AppUser SET IsActive = @isActive WHERE UserName = @userName AND TenantId IS NOT NULL",
            new { userName, isActive }, cancellationToken: ct)) > 0;
    }

    public async Task<bool> SetUserPasswordAsync(string userName, string hash, string salt, CancellationToken ct = default)
    {
        using var c = await _f.CreateOpenConnectionAsync(ct);
        return await c.ExecuteAsync(new CommandDefinition(
            "UPDATE security.AppUser SET PasswordHash = @hash, PasswordSalt = @salt WHERE UserName = @userName AND TenantId IS NOT NULL",
            new { userName, hash, salt }, cancellationToken: ct)) > 0;
    }

    public async Task ReplaceUserRoleAsync(long userId, int roleId, CancellationToken ct = default)
    {
        using var c = await _f.CreateOpenConnectionAsync(ct);
        await c.ExecuteAsync(new CommandDefinition(
            @"DELETE FROM security.UserRole WHERE UserId = @userId;
              INSERT security.UserRole (UserId, RoleId) VALUES (@userId, @roleId);",
            new { userId, roleId }, cancellationToken: ct));
    }

    public async Task<IReadOnlyList<(string Code, int RoleId)>> GetRoleIdsByCodesAsync(IEnumerable<string> codes, CancellationToken ct = default)
    {
        var list = codes.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        if (list.Length == 0) return System.Array.Empty<(string, int)>();
        using var c = await _f.CreateOpenConnectionAsync(ct);
        // SQL Server's default collation is case-insensitive, so 'Doctor' matches 'doctor'.
        return (await c.QueryAsync<(string Code, int RoleId)>(new CommandDefinition(
            "SELECT Code, RoleId FROM security.Role WHERE Code IN @codes",
            new { codes = list }, cancellationToken: ct))).ToList();
    }

    public async Task ReplaceUserRolesAsync(long userId, IReadOnlyCollection<int> roleIds, CancellationToken ct = default)
    {
        using var c = await _f.CreateOpenConnectionAsync(ct);
        using var tx = c.BeginTransaction();
        await c.ExecuteAsync(new CommandDefinition(
            "DELETE FROM security.UserRole WHERE UserId = @userId;",
            new { userId }, transaction: tx, cancellationToken: ct));
        if (roleIds.Count > 0)
            // Dapper runs the parameterised INSERT once per RoleId (multi-exec), all inside the txn.
            await c.ExecuteAsync(new CommandDefinition(
                "INSERT security.UserRole (UserId, RoleId) VALUES (@userId, @roleId);",
                roleIds.Distinct().Select(roleId => new { userId, roleId }), transaction: tx, cancellationToken: ct));
        tx.Commit();
    }

    public async Task<bool> HasPrivilegedRoleAsync(long userId, CancellationToken ct = default)
    {
        using var c = await _f.CreateOpenConnectionAsync(ct);
        return await c.ExecuteScalarAsync<int>(new CommandDefinition(
            @"SELECT COUNT(1) FROM security.UserRole ur
              INNER JOIN security.Role r ON r.RoleId = ur.RoleId
              WHERE ur.UserId = @userId AND r.IsPrivileged = 1", new { userId }, cancellationToken: ct)) > 0;
    }

    public async Task SetMfaSecretAsync(long userId, string secret, CancellationToken ct = default)
    {
        using var c = await _f.CreateOpenConnectionAsync(ct);
        // Encrypt the TOTP secret at rest (AES-256-GCM) before persisting.
        var stored = _protector.Protect(secret);
        await c.ExecuteAsync(new CommandDefinition(
            "UPDATE security.AppUser SET MfaSecret = @stored, MfaEnabled = 1 WHERE UserId = @userId",
            new { userId, stored }, cancellationToken: ct));
    }

    public async Task WritePlatformAuditAsync(long? actorUserId, string? actorUserName, int? tenantId,
        string action, string entity, string? entityId, bool succeeded, string? error, CancellationToken ct = default)
    {
        using var c = await _f.CreateOpenConnectionAsync(ct);
        const string sql = @"
INSERT INTO audit.PlatformAudit (ActorUserId, ActorUserName, TenantId, Action, Entity, EntityId, Succeeded, Error)
VALUES (@actorUserId, @actorUserName, @tenantId, @action, @entity, @entityId, @succeeded, @error);";
        await c.ExecuteAsync(new CommandDefinition(sql,
            new { actorUserId, actorUserName, tenantId, action, entity, entityId, succeeded, error },
            cancellationToken: ct));
    }

    public async Task<IReadOnlyList<(DateTime, string?, string, string, string?, bool)>> GetRecentAuditAsync(int take, CancellationToken ct = default)
    {
        using var c = await _f.CreateOpenConnectionAsync(ct);
        var rows = await c.QueryAsync<(DateTime, string?, string, string, string?, bool)>(new CommandDefinition(
            @"SELECT TOP (@take) OccurredUtc, ActorUserName, Action, Entity, EntityId, Succeeded
              FROM audit.PlatformAudit ORDER BY AuditId DESC", new { take }, cancellationToken: ct));
        return rows.ToList();
    }
}

/// <summary>Dynamic module/page registry + assignments + menu over HIS_Platform.security.* (L1.3).</summary>
public sealed class ModuleAdminRepository : IModuleAdminRepository
{
    private readonly IPlatformConnectionFactory _f;
    public ModuleAdminRepository(IPlatformConnectionFactory f) => _f = f;

    public async Task<int> CreateModuleAsync(string code, string label, string? icon, int sortOrder, CancellationToken ct = default)
    {
        using var c = await _f.CreateOpenConnectionAsync(ct);
        return await c.QuerySingleAsync<int>(new CommandDefinition(
            @"INSERT INTO security.AppModule (Code, Label, Icon, SortOrder)
              VALUES (@code, @label, @icon, @sortOrder);
              SELECT CAST(SCOPE_IDENTITY() AS INT);",
            new { code, label, icon, sortOrder }, cancellationToken: ct));
    }

    public async Task<int> CreatePageAsync(string moduleCode, string code, string label, string? route, int sortOrder, CancellationToken ct = default)
    {
        using var c = await _f.CreateOpenConnectionAsync(ct);
        return await c.QuerySingleAsync<int>(new CommandDefinition(
            @"DECLARE @ModuleId INT = (SELECT ModuleId FROM security.AppModule WHERE Code = @moduleCode);
              IF @ModuleId IS NULL THROW 50000, 'Unknown module code.', 1;
              INSERT INTO security.AppPage (ModuleId, Code, Label, Route, SortOrder)
              VALUES (@ModuleId, @code, @label, @route, @sortOrder);
              DECLARE @PageId INT = CAST(SCOPE_IDENTITY() AS INT);
              -- Seed the standard CRUD page-actions (L1.3.3) so they can be granted to roles
              -- immediately — there is no separate 'create action' step in the admin console.
              INSERT INTO security.PageAction (PageId, Code, Label) VALUES
                  (@PageId, 'view', 'View'), (@PageId, 'create', 'Create'),
                  (@PageId, 'edit', 'Edit'), (@PageId, 'delete', 'Delete');
              SELECT @PageId;",
            new { moduleCode, code, label, route, sortOrder }, cancellationToken: ct));
    }

    public async Task<bool> AssignModuleToRoleAsync(string roleCode, string moduleCode, CancellationToken ct = default)
    {
        using var c = await _f.CreateOpenConnectionAsync(ct);
        var n = await c.ExecuteAsync(new CommandDefinition(
            @"DECLARE @RoleId INT = (SELECT RoleId FROM security.Role WHERE Code = @roleCode);
              DECLARE @ModuleId INT = (SELECT ModuleId FROM security.AppModule WHERE Code = @moduleCode);
              IF @RoleId IS NULL OR @ModuleId IS NULL THROW 50000, 'Unknown role or module code.', 1;
              IF NOT EXISTS (SELECT 1 FROM security.RoleModule WHERE RoleId = @RoleId AND ModuleId = @ModuleId)
                  INSERT security.RoleModule (RoleId, ModuleId) VALUES (@RoleId, @ModuleId);",
            new { roleCode, moduleCode }, cancellationToken: ct));
        return n > 0;
    }

    public async Task<bool> AssignPageToRoleAsync(string roleCode, string pageCode, CancellationToken ct = default)
    {
        using var c = await _f.CreateOpenConnectionAsync(ct);
        var n = await c.ExecuteAsync(new CommandDefinition(
            @"DECLARE @RoleId INT = (SELECT RoleId FROM security.Role WHERE Code = @roleCode);
              DECLARE @PageId INT = (SELECT PageId FROM security.AppPage WHERE Code = @pageCode);
              IF @RoleId IS NULL OR @PageId IS NULL THROW 50000, 'Unknown role or page code.', 1;
              IF NOT EXISTS (SELECT 1 FROM security.RolePage WHERE RoleId = @RoleId AND PageId = @PageId)
                  INSERT security.RolePage (RoleId, PageId) VALUES (@RoleId, @PageId);",
            new { roleCode, pageCode }, cancellationToken: ct));
        return n > 0;
    }

    public async Task<bool> AssignPageActionToRoleAsync(string roleCode, string pageCode, string actionCode, CancellationToken ct = default)
    {
        using var c = await _f.CreateOpenConnectionAsync(ct);
        var n = await c.ExecuteAsync(new CommandDefinition(
            @"DECLARE @RoleId INT = (SELECT RoleId FROM security.Role WHERE Code = @roleCode);
              DECLARE @ActionId INT = (SELECT a.ActionId FROM security.PageAction a
                                       INNER JOIN security.AppPage p ON p.PageId = a.PageId
                                       WHERE p.Code = @pageCode AND a.Code = @actionCode);
              IF @RoleId IS NULL OR @ActionId IS NULL THROW 50000, 'Unknown role, page or action code.', 1;
              IF NOT EXISTS (SELECT 1 FROM security.RolePageAction WHERE RoleId = @RoleId AND ActionId = @ActionId)
                  INSERT security.RolePageAction (RoleId, ActionId) VALUES (@RoleId, @ActionId);",
            new { roleCode, pageCode, actionCode }, cancellationToken: ct));
        return n > 0;
    }

    private const string MenuProjection =
        @"SELECT m.Code AS ModuleCode, m.Label AS ModuleLabel, m.Icon, m.SortOrder AS ModuleSort,
                 p.Code AS PageCode, p.Label AS PageLabel, p.Route, p.SortOrder AS PageSort";

    public async Task<IReadOnlyList<(string, string, string?, int, string, string, string?, int)>> GetFullMenuAsync(CancellationToken ct = default)
    {
        using var c = await _f.CreateOpenConnectionAsync(ct);
        var rows = await c.QueryAsync<(string, string, string?, int, string, string, string?, int)>(new CommandDefinition(
            MenuProjection + @"
              FROM security.AppModule m
              INNER JOIN security.AppPage p ON p.ModuleId = m.ModuleId
              WHERE m.IsActive = 1 AND p.IsActive = 1
              ORDER BY m.SortOrder, p.SortOrder", cancellationToken: ct));
        return rows.ToList();
    }

    public async Task<IReadOnlyList<(string, string, string?, int, string, string, string?, int)>> GetMenuForRolesAsync(IEnumerable<string> roleCodes, int? tenantId, int? fiscalYearId, CancellationToken ct = default)
    {
        var codes = roleCodes?.Where(r => !string.IsNullOrWhiteSpace(r)).Distinct().ToArray() ?? Array.Empty<string>();
        if (codes.Length == 0) return Array.Empty<(string, string, string?, int, string, string, string?, int)>();

        using var c = await _f.CreateOpenConnectionAsync(ct);
        // A page is visible if any of the user's roles grants its module (RoleModule) or the
        // page itself (RolePage) — AND, when a tenant + fiscal year is resolved, the module is
        // entitled for that tenant/FY (platform.TenantModule.Enabled, L1.3.5/R3). With no tenant
        // resolved (@tenantId/@fyId NULL) the entitlement filter is skipped (role-only).
        var rows = await c.QueryAsync<(string, string, string?, int, string, string, string?, int)>(new CommandDefinition(
            MenuProjection + @"
              FROM security.AppModule m
              INNER JOIN security.AppPage p ON p.ModuleId = m.ModuleId
              WHERE m.IsActive = 1 AND p.IsActive = 1
                AND (
                  EXISTS (SELECT 1 FROM security.RoleModule rm
                          INNER JOIN security.Role r ON r.RoleId = rm.RoleId
                          WHERE rm.ModuleId = m.ModuleId AND r.Code IN @codes)
                  OR EXISTS (SELECT 1 FROM security.RolePage rp
                          INNER JOIN security.Role r ON r.RoleId = rp.RoleId
                          WHERE rp.PageId = p.PageId AND r.Code IN @codes)
                )
                AND (@tenantId IS NULL OR @fiscalYearId IS NULL OR EXISTS (
                      SELECT 1 FROM platform.TenantModule tm
                      WHERE tm.TenantId = @tenantId AND tm.FiscalYearId = @fiscalYearId
                        AND tm.ModuleId = m.ModuleId AND tm.Enabled = 1))
              ORDER BY m.SortOrder, p.SortOrder", new { codes, tenantId, fiscalYearId }, cancellationToken: ct));
        return rows.ToList();
    }
}

/// <summary>Control-plane tenant/fiscal-year/domain/db-catalog store (L1.7) over HIS_Platform.platform.*.</summary>
public sealed class TenantAdminRepository : ITenantAdminRepository
{
    private readonly IPlatformConnectionFactory _f;
    public TenantAdminRepository(IPlatformConnectionFactory f) => _f = f;

    public async Task<bool> TenantExistsAsync(string code, CancellationToken ct = default)
    {
        using var c = await _f.CreateOpenConnectionAsync(ct);
        return await c.ExecuteScalarAsync<int>(new CommandDefinition(
            "SELECT COUNT(1) FROM platform.Tenant WHERE Code = @code", new { code }, cancellationToken: ct)) > 0;
    }

    public async Task<IReadOnlyList<string>> GetTenantDatabaseNamesAsync(int tenantId, CancellationToken ct = default)
    {
        using var c = await _f.CreateOpenConnectionAsync(ct);
        return (await c.QueryAsync<string>(new CommandDefinition(
            "SELECT DISTINCT DbName FROM platform.DbCatalog WHERE TenantId = @tenantId",
            new { tenantId }, cancellationToken: ct))).ToList();
    }

    public async Task DeleteTenantAsync(int tenantId, CancellationToken ct = default)
    {
        using var c = await _f.CreateOpenConnectionAsync(ct);
        // Child-first; audit.PlatformAudit is intentionally retained (immutable trail).
        await c.ExecuteAsync(new CommandDefinition(@"
DELETE ur FROM security.UserRole ur JOIN security.AppUser u ON u.UserId = ur.UserId WHERE u.TenantId = @t;
DELETE FROM security.AppUser       WHERE TenantId = @t;
DELETE FROM platform.TenantModule  WHERE TenantId = @t;
DELETE FROM platform.Subscription  WHERE TenantId = @t;
DELETE FROM platform.BillingLedger WHERE TenantId = @t;
DELETE FROM platform.DbCatalog     WHERE TenantId = @t;
DELETE FROM platform.TenantDomain  WHERE TenantId = @t;
DELETE FROM platform.FiscalYear    WHERE TenantId = @t;
DELETE FROM platform.Tenant        WHERE TenantId = @t;",
            new { t = tenantId }, cancellationToken: ct));
    }

    public async Task<int> CreateTenantAsync(string code, string name, byte fyStartMonth, byte fyStartDay, CancellationToken ct = default)
    {
        using var c = await _f.CreateOpenConnectionAsync(ct);
        return await c.QuerySingleAsync<int>(new CommandDefinition(
            @"INSERT INTO platform.Tenant (Code, Name, FyStartMonth, FyStartDay)
              VALUES (@code, @name, @fyStartMonth, @fyStartDay);
              SELECT CAST(SCOPE_IDENTITY() AS INT);",
            new { code, name, fyStartMonth, fyStartDay }, cancellationToken: ct));
    }

    public async Task<int?> GetTenantIdByCodeAsync(string code, CancellationToken ct = default)
    {
        using var c = await _f.CreateOpenConnectionAsync(ct);
        return await c.QuerySingleOrDefaultAsync<int?>(new CommandDefinition(
            "SELECT TenantId FROM platform.Tenant WHERE Code = @code", new { code }, cancellationToken: ct));
    }

    public async Task<bool> FiscalYearExistsAsync(int tenantId, string fyCode, CancellationToken ct = default)
    {
        using var c = await _f.CreateOpenConnectionAsync(ct);
        return await c.ExecuteScalarAsync<int>(new CommandDefinition(
            "SELECT COUNT(1) FROM platform.FiscalYear WHERE TenantId = @tenantId AND Code = @fyCode",
            new { tenantId, fyCode }, cancellationToken: ct)) > 0;
    }

    public async Task<int> CreateFiscalYearAsync(int tenantId, string code, DateTime start, DateTime end, bool isCurrent, CancellationToken ct = default)
    {
        using var c = await _f.CreateOpenConnectionAsync(ct);
        return await c.QuerySingleAsync<int>(new CommandDefinition(
            @"INSERT INTO platform.FiscalYear (TenantId, Code, StartDate, EndDate, IsCurrent)
              VALUES (@tenantId, @code, @start, @end, @isCurrent);
              SELECT CAST(SCOPE_IDENTITY() AS INT);",
            new { tenantId, code, start, end, isCurrent }, cancellationToken: ct));
    }

    public async Task ClearCurrentFiscalYearAsync(int tenantId, CancellationToken ct = default)
    {
        using var c = await _f.CreateOpenConnectionAsync(ct);
        await c.ExecuteAsync(new CommandDefinition(
            "UPDATE platform.FiscalYear SET IsCurrent = 0 WHERE TenantId = @tenantId", new { tenantId }, cancellationToken: ct));
    }

    public async Task<int?> GetCurrentFiscalYearIdAsync(int tenantId, CancellationToken ct = default)
    {
        using var c = await _f.CreateOpenConnectionAsync(ct);
        return await c.QuerySingleOrDefaultAsync<int?>(new CommandDefinition(
            "SELECT FiscalYearId FROM platform.FiscalYear WHERE TenantId = @tenantId AND IsCurrent = 1",
            new { tenantId }, cancellationToken: ct));
    }

    public async Task AddDomainAsync(int tenantId, string host, bool isPrimary, bool isCommon, CancellationToken ct = default)
    {
        using var c = await _f.CreateOpenConnectionAsync(ct);
        await c.ExecuteAsync(new CommandDefinition(
            @"IF NOT EXISTS (SELECT 1 FROM platform.TenantDomain WHERE Host = @host)
                  INSERT platform.TenantDomain (TenantId, Host, IsPrimary, IsCommon)
                  VALUES (@tenantId, @host, @isPrimary, @isCommon);",
            new { tenantId, host, isPrimary, isCommon }, cancellationToken: ct));
    }

    public async Task RegisterDbAsync(int tenantId, int? fiscalYearId, string dbKind, string dbName, string? connectionRef, CancellationToken ct = default)
    {
        using var c = await _f.CreateOpenConnectionAsync(ct);
        await c.ExecuteAsync(new CommandDefinition(
            @"IF NOT EXISTS (SELECT 1 FROM platform.DbCatalog
                             WHERE TenantId = @tenantId AND DbKind = @dbKind
                               AND ISNULL(FiscalYearId,-1) = ISNULL(@fiscalYearId,-1))
                  INSERT platform.DbCatalog (TenantId, FiscalYearId, DbKind, DbName, ConnectionRef)
                  VALUES (@tenantId, @fiscalYearId, @dbKind, @dbName, @connectionRef);",
            new { tenantId, fiscalYearId, dbKind, dbName, connectionRef }, cancellationToken: ct));
    }

    public async Task EnableAllModulesAsync(int tenantId, int fiscalYearId, CancellationToken ct = default)
    {
        using var c = await _f.CreateOpenConnectionAsync(ct);
        await c.ExecuteAsync(new CommandDefinition(
            @"INSERT platform.TenantModule (TenantId, FiscalYearId, ModuleId, Enabled)
              SELECT @tenantId, @fiscalYearId, m.ModuleId, 1
              FROM security.AppModule m
              WHERE m.IsActive = 1
                AND NOT EXISTS (SELECT 1 FROM platform.TenantModule tm
                                WHERE tm.TenantId = @tenantId AND tm.FiscalYearId = @fiscalYearId AND tm.ModuleId = m.ModuleId);",
            new { tenantId, fiscalYearId }, cancellationToken: ct));
    }

    public async Task CopyModuleEntitlementsAsync(int tenantId, int fromFiscalYearId, int toFiscalYearId, CancellationToken ct = default)
    {
        using var c = await _f.CreateOpenConnectionAsync(ct);
        await c.ExecuteAsync(new CommandDefinition(
            @"INSERT platform.TenantModule (TenantId, FiscalYearId, ModuleId, Enabled)
              SELECT @tenantId, @toFiscalYearId, tm.ModuleId, tm.Enabled
              FROM platform.TenantModule tm
              WHERE tm.TenantId = @tenantId AND tm.FiscalYearId = @fromFiscalYearId
                AND NOT EXISTS (SELECT 1 FROM platform.TenantModule x
                                WHERE x.TenantId = @tenantId AND x.FiscalYearId = @toFiscalYearId AND x.ModuleId = tm.ModuleId);",
            new { tenantId, fromFiscalYearId, toFiscalYearId }, cancellationToken: ct));
    }

    public async Task<bool> SetTenantModuleAsync(string tenantCode, string fyCode, string moduleCode, bool enabled, CancellationToken ct = default)
    {
        using var c = await _f.CreateOpenConnectionAsync(ct);
        // Upsert the per-tenant, per-FY module entitlement (L1.3.3/L1.4.4).
        var n = await c.ExecuteAsync(new CommandDefinition(
            @"DECLARE @TenantId INT = (SELECT TenantId FROM platform.Tenant WHERE Code = @tenantCode);
              DECLARE @FyId INT = (SELECT FiscalYearId FROM platform.FiscalYear WHERE TenantId = @TenantId AND Code = @fyCode);
              DECLARE @ModuleId INT = (SELECT ModuleId FROM security.AppModule WHERE Code = @moduleCode);
              IF @TenantId IS NULL OR @FyId IS NULL OR @ModuleId IS NULL
                  THROW 50000, 'Unknown tenant, fiscal year or module code.', 1;
              IF EXISTS (SELECT 1 FROM platform.TenantModule WHERE TenantId=@TenantId AND FiscalYearId=@FyId AND ModuleId=@ModuleId)
                  UPDATE platform.TenantModule SET Enabled = @enabled
                  WHERE TenantId=@TenantId AND FiscalYearId=@FyId AND ModuleId=@ModuleId;
              ELSE
                  INSERT platform.TenantModule (TenantId, FiscalYearId, ModuleId, Enabled)
                  VALUES (@TenantId, @FyId, @ModuleId, @enabled);",
            new { tenantCode, fyCode, moduleCode, enabled }, cancellationToken: ct));
        return n > 0;
    }

    public async Task CreateSubscriptionAsync(int tenantId, int fiscalYearId, string plan, DateTime start, DateTime end, CancellationToken ct = default)
    {
        using var c = await _f.CreateOpenConnectionAsync(ct);
        // One subscription per (tenant, fiscal year); idempotent so a retried onboarding/year-shift is safe.
        await c.ExecuteAsync(new CommandDefinition(
            @"IF NOT EXISTS (SELECT 1 FROM platform.Subscription WHERE TenantId = @tenantId AND FiscalYearId = @fiscalYearId)
                  INSERT platform.Subscription (TenantId, FiscalYearId, [Plan], StartDate, EndDate, Status)
                  VALUES (@tenantId, @fiscalYearId, @plan, @start, @end, 'Active');",
            new { tenantId, fiscalYearId, plan, start, end }, cancellationToken: ct));
    }

    public async Task WriteBillingLedgerAsync(int tenantId, int fiscalYearId, string entryType, decimal amount, string currency, string? notes, CancellationToken ct = default)
    {
        using var c = await _f.CreateOpenConnectionAsync(ct);
        await c.ExecuteAsync(new CommandDefinition(
            @"INSERT platform.BillingLedger (TenantId, FiscalYearId, EntryType, Amount, Currency, Notes)
              VALUES (@tenantId, @fiscalYearId, @entryType, @amount, @currency, @notes);",
            new { tenantId, fiscalYearId, entryType, amount, currency, notes }, cancellationToken: ct));
    }

    public async Task<decimal> GetLedgerBalanceAsync(int tenantId, int fiscalYearId, CancellationToken ct = default)
    {
        using var c = await _f.CreateOpenConnectionAsync(ct);
        return await c.ExecuteScalarAsync<decimal>(new CommandDefinition(
            "SELECT ISNULL(SUM(Amount),0) FROM platform.BillingLedger WHERE TenantId = @tenantId AND FiscalYearId = @fiscalYearId",
            new { tenantId, fiscalYearId }, cancellationToken: ct));
    }

    public async Task<IReadOnlyList<(int, string, string, string?, string, string)>> GetTenantsAsync(CancellationToken ct = default)
    {
        using var c = await _f.CreateOpenConnectionAsync(ct);
        var rows = await c.QueryAsync<(int, string, string, string?, string, string)>(new CommandDefinition(
            @"SELECT t.TenantId, t.Code, t.Name, fy.Code AS FyCode, d.DbKind, d.DbName
              FROM platform.Tenant t
              LEFT JOIN platform.DbCatalog d ON d.TenantId = t.TenantId
              LEFT JOIN platform.FiscalYear fy ON fy.FiscalYearId = d.FiscalYearId
              ORDER BY t.TenantId, d.DbKind", cancellationToken: ct));
        return rows.ToList();
    }

    public async Task<(int, string)?> ResolveTenantByHostAsync(string host, CancellationToken ct = default)
    {
        using var c = await _f.CreateOpenConnectionAsync(ct);
        var row = await c.QuerySingleOrDefaultAsync<(int, string)?>(new CommandDefinition(
            @"SELECT TOP 1 t.TenantId, t.Code
              FROM platform.TenantDomain d INNER JOIN platform.Tenant t ON t.TenantId = d.TenantId
              WHERE d.Host = @host", new { host }, cancellationToken: ct));
        return row;
    }

    // Routing projection: tenant + master DB + current-FY data DB (current fiscal year).
    private const string RoutingSql = @"
        SELECT t.TenantId, t.Code,
               dm.DbName AS MasterDb,
               fy.FiscalYearId AS CurrentFiscalYearId, fy.Code AS CurrentFiscalYearCode,
               dd.DbName AS DataDb
        FROM platform.Tenant t
        LEFT JOIN platform.DbCatalog dm ON dm.TenantId = t.TenantId AND dm.DbKind = 'master'
        LEFT JOIN platform.FiscalYear fy ON fy.TenantId = t.TenantId AND fy.IsCurrent = 1
        LEFT JOIN platform.DbCatalog dd ON dd.TenantId = t.TenantId AND dd.DbKind = 'data' AND dd.FiscalYearId = fy.FiscalYearId";

    public async Task<TenantRouting?> GetRoutingByHostAsync(string host, CancellationToken ct = default)
    {
        using var c = await _f.CreateOpenConnectionAsync(ct);
        return await c.QuerySingleOrDefaultAsync<TenantRouting>(new CommandDefinition(
            RoutingSql + @"
              WHERE t.TenantId = (SELECT TOP 1 TenantId FROM platform.TenantDomain WHERE Host = @host)",
            new { host }, cancellationToken: ct));
    }

    public async Task<TenantRouting?> GetRoutingByCodeAsync(string code, CancellationToken ct = default)
    {
        using var c = await _f.CreateOpenConnectionAsync(ct);
        return await c.QuerySingleOrDefaultAsync<TenantRouting>(new CommandDefinition(
            RoutingSql + " WHERE t.Code = @code", new { code }, cancellationToken: ct));
    }
}

/// <summary>Resolves permission codes for role codes from HIS_Platform.security.* (L1.2.6).</summary>
public sealed class PermissionResolver : IPermissionResolver
{
    private readonly IPlatformConnectionFactory _f;
    public PermissionResolver(IPlatformConnectionFactory f) => _f = f;

    public async Task<IReadOnlySet<string>> GetPermissionsAsync(IEnumerable<string> roleCodes, CancellationToken ct = default)
    {
        var codes = roleCodes?.Where(r => !string.IsNullOrWhiteSpace(r)).Distinct().ToArray() ?? Array.Empty<string>();
        if (codes.Length == 0) return new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        using var c = await _f.CreateOpenConnectionAsync(ct);
        var perms = await c.QueryAsync<string>(new CommandDefinition(
            @"SELECT DISTINCT p.Code
              FROM security.Role r
              INNER JOIN security.RolePermission rp ON rp.RoleId = r.RoleId
              INNER JOIN security.Permission p ON p.PermissionId = rp.PermissionId
              WHERE r.Code IN @codes", new { codes }, cancellationToken: ct));
        return new HashSet<string>(perms, StringComparer.OrdinalIgnoreCase);
    }
}
