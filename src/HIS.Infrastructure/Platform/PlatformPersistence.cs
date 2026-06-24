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
    public PlatformUserRepository(IPlatformConnectionFactory f) => _f = f;

    public async Task<PlatformUser?> GetByUserNameAsync(string userName, CancellationToken ct = default)
    {
        using var c = await _f.CreateOpenConnectionAsync(ct);
        return await c.QuerySingleOrDefaultAsync<PlatformUser>(new CommandDefinition(
            @"SELECT UserId, TenantId, UserName, DisplayName, Email, PasswordHash, PasswordSalt,
                     IsSuperAdmin, MfaEnabled, IsActive
              FROM security.AppUser WHERE UserName = @userName",
            new { userName }, cancellationToken: ct));
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
              SELECT CAST(SCOPE_IDENTITY() AS INT);",
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

    public async Task<IReadOnlyList<(string, string, string?, int, string, string, string?, int)>> GetMenuForRolesAsync(IEnumerable<string> roleCodes, CancellationToken ct = default)
    {
        var codes = roleCodes?.Where(r => !string.IsNullOrWhiteSpace(r)).Distinct().ToArray() ?? Array.Empty<string>();
        if (codes.Length == 0) return Array.Empty<(string, string, string?, int, string, string, string?, int)>();

        using var c = await _f.CreateOpenConnectionAsync(ct);
        // A page is visible if any of the user's roles grants its module (RoleModule)
        // or the page itself (RolePage).
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
              ORDER BY m.SortOrder, p.SortOrder", new { codes }, cancellationToken: ct));
        return rows.ToList();
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
