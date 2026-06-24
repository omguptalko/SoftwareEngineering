using HIS.Application.Abstractions;
using HIS.Domain.Entities;

namespace HIS.Api.Startup;

/// <summary>
/// Ensures a platform superadmin exists (L1.2). On startup, if no user with the
/// configured bootstrap username exists in HIS_Platform.security.AppUser, it
/// creates one (PBKDF2-hashed password) and assigns the 'superadmin' role.
/// Credentials come from config ("Platform:Bootstrap:*") — never hardcoded.
/// Idempotent: a no-op once the superadmin exists.
/// </summary>
public sealed class SuperAdminSeeder : IHostedService
{
    private readonly IServiceScopeFactory _scopes;
    private readonly IConfiguration _config;
    private readonly ILogger<SuperAdminSeeder> _log;

    public SuperAdminSeeder(IServiceScopeFactory scopes, IConfiguration config, ILogger<SuperAdminSeeder> log)
    { _scopes = scopes; _config = config; _log = log; }

    public async Task StartAsync(CancellationToken ct)
    {
        var userName = _config["Platform:Bootstrap:UserName"];
        var password = _config["Platform:Bootstrap:Password"];
        var display  = _config["Platform:Bootstrap:DisplayName"] ?? "Super Admin";
        var email    = _config["Platform:Bootstrap:Email"];

        if (string.IsNullOrWhiteSpace(userName) || string.IsNullOrWhiteSpace(password))
        {
            _log.LogInformation("SuperAdmin bootstrap skipped: 'Platform:Bootstrap:UserName/Password' not configured.");
            return;
        }

        using var scope = _scopes.CreateScope();
        var users  = scope.ServiceProvider.GetRequiredService<IPlatformUserRepository>();
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();

        try
        {
            if (await users.GetByUserNameAsync(userName, ct) is null)
            {
                var (hash, salt) = hasher.Hash(password);
                var userId = await users.InsertUserAsync(new PlatformUser
                {
                    TenantId = null,
                    UserName = userName,
                    DisplayName = display,
                    Email = email,
                    PasswordHash = hash,
                    PasswordSalt = salt,
                    IsSuperAdmin = true,
                    IsActive = true
                }, ct);

                var roleId = await users.GetRoleIdByCodeAsync("superadmin", ct)
                    ?? throw new InvalidOperationException("Role 'superadmin' is not seeded (run db/platform migrations).");
                await users.AssignRoleAsync(userId, roleId, ct);

                await users.WritePlatformAuditAsync(userId, userName, null, "SeedSuperAdmin", "AppUser",
                    userId.ToString(), succeeded: true, error: null, ct);

                _log.LogInformation("SuperAdmin '{User}' created (id {Id}).", userName, userId);
            }
            else
            {
                _log.LogInformation("SuperAdmin '{User}' already present.", userName);
            }

            // Optional dev demo user (non-privileged) to exercise RBAC 403 paths.
            await SeedDemoUserAsync(users, hasher, ct);

            // Optional dev tenant — auto-provisioned so localhost (the wireframe) routes
            // to a real per-tenant DB for cut-over endpoints (L1.8).
            await SeedDevTenantAsync(scope, ct);
        }
        catch (Exception ex)
        {
            // Don't crash the app if the platform DB isn't migrated yet; log and continue.
            _log.LogWarning(ex, "Platform bootstrap failed (is HIS_Platform migrated?).");
        }
    }

    private async Task SeedDemoUserAsync(IPlatformUserRepository users, IPasswordHasher hasher, CancellationToken ct)
    {
        var demoUser = _config["Platform:Bootstrap:DemoUser:UserName"];
        var demoPass = _config["Platform:Bootstrap:DemoUser:Password"];
        var demoRole = _config["Platform:Bootstrap:DemoUser:Role"] ?? "billing";
        if (string.IsNullOrWhiteSpace(demoUser) || string.IsNullOrWhiteSpace(demoPass)) return;
        if (await users.GetByUserNameAsync(demoUser, ct) is not null) return;

        var (hash, salt) = hasher.Hash(demoPass);
        var userId = await users.InsertUserAsync(new PlatformUser
        {
            TenantId = null,
            UserName = demoUser,
            DisplayName = _config["Platform:Bootstrap:DemoUser:DisplayName"] ?? demoUser,
            PasswordHash = hash,
            PasswordSalt = salt,
            IsSuperAdmin = false,
            IsActive = true
        }, ct);

        var roleId = await users.GetRoleIdByCodeAsync(demoRole, ct);
        if (roleId is int rid) await users.AssignRoleAsync(userId, rid, ct);
        _log.LogInformation("Demo user '{User}' (role {Role}) created (id {Id}).", demoUser, demoRole, userId);
    }

    /// <summary>Provisions a dev tenant (mirrors OnboardTenantHandler) so localhost is tenant-routed.</summary>
    private async Task SeedDevTenantAsync(IServiceScope scope, CancellationToken ct)
    {
        var code = _config["Tenancy:DevDefaultTenant"];
        if (string.IsNullOrWhiteSpace(code)) return;

        var tenants = scope.ServiceProvider.GetRequiredService<ITenantAdminRepository>();
        if (await tenants.TenantExistsAsync(code, ct))
        {
            _log.LogInformation("Dev tenant '{Code}' already present.", code);
            return;
        }

        var engine = scope.ServiceProvider.GetRequiredService<IProvisioningEngine>();

        // Fiscal year (Apr-Mar, D1) for "now".
        var now = DateTime.UtcNow;
        var startYear = now.Month >= 4 ? now.Year : now.Year - 1;
        var start = new DateTime(startYear, 4, 1);
        var end = start.AddYears(1).AddDays(-1);
        var fyCode = $"FY{startYear}-{((startYear + 1) % 100):D2}";

        var master = await engine.ProvisionMasterAsync(code, ct);
        var data = await engine.ProvisionFiscalYearAsync(code, fyCode, ct);

        var tenantId = await tenants.CreateTenantAsync(code, $"{code} (dev tenant)", 4, 1, ct);
        var fyId = await tenants.CreateFiscalYearAsync(tenantId, fyCode, start, end, isCurrent: true, ct);
        await tenants.AddDomainAsync(tenantId, $"{code.ToLowerInvariant()}.localhost", isPrimary: true, isCommon: false, ct);
        await tenants.RegisterDbAsync(tenantId, null, master.DbKind, master.DbName, $"Tenant:{code}:Master", ct);
        await tenants.RegisterDbAsync(tenantId, fyId, data.DbKind, data.DbName, $"Tenant:{code}:{fyCode}", ct);
        await tenants.EnableAllModulesAsync(tenantId, fyId, ct);

        _log.LogInformation("Dev tenant '{Code}' provisioned ({Master} + {Data}).", code, master.DbName, data.DbName);
    }

    public Task StopAsync(CancellationToken ct) => Task.CompletedTask;
}
