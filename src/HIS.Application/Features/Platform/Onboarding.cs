using FluentValidation;
using HIS.Application.Abstractions;
using HIS.Shared.Context;
using Microsoft.Extensions.Configuration;

namespace HIS.Application.Features.Platform;

internal static class FiscalCalc
{
    /// <summary>Builds a fiscal-year code + date range from a start year and the tenant's
    /// FY-start (Decision D1, default Apr 1). e.g. 2026 → ("FY2026-27", 2026-04-01, 2027-03-31).</summary>
    public static (string Code, DateTime Start, DateTime End) Build(int startYear, byte fyStartMonth, byte fyStartDay)
    {
        var start = new DateTime(startYear, fyStartMonth, fyStartDay);
        var end = start.AddYears(1).AddDays(-1);
        var code = $"FY{startYear}-{((startYear + 1) % 100):D2}";
        return (code, start, end);
    }
}

/// <summary>Per-FY subscription billing defaults — config-driven (nothing hardcoded), L1.4.2.</summary>
internal static class BillingDefaults
{
    public static (string Plan, decimal AnnualFee, string Currency) Read(IConfiguration config) => (
        config["Platform:Billing:DefaultPlan"] ?? "Standard",
        config.GetValue("Platform:Billing:AnnualFee", 0m),
        config["Platform:Billing:Currency"] ?? "INR");
}

// ============================ Onboard tenant (tenant.onboard) ============================
/// <summary>
/// Onboards a hospital on the SaaS (L1.7, R3/R4/R5): registers the tenant + fiscal year +
/// domain(s), then auto-provisions {Tenant}_Master + {Tenant}_FY{code} databases (no human
/// step), registers them in the DB catalog and enables all modules for the year.
/// </summary>
public sealed record OnboardTenantCommand(
    string Code, string Name, int FiscalYearStart, string PrimaryHost, string? CommonHost)
    : ICommand<OnboardTenantResult>, IAuthorizable
{
    public string RequiredPermission => "tenant.onboard";
}
public sealed record OnboardTenantResult(int TenantId, int FiscalYearId, string FiscalYearCode, string MasterDb, string DataDb);

public sealed class OnboardTenantValidator : AbstractValidator<OnboardTenantCommand>
{
    public OnboardTenantValidator()
    {
        RuleFor(x => x.Code).NotEmpty().Matches("^[A-Za-z0-9-]+$").MaximumLength(40);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(160);
        RuleFor(x => x.FiscalYearStart).InclusiveBetween(2000, 2100);
        RuleFor(x => x.PrimaryHost).NotEmpty().MaximumLength(190);
    }
}

public sealed class OnboardTenantHandler : MediatR.IRequestHandler<OnboardTenantCommand, OnboardTenantResult>
{
    private const byte FyMonth = 4, FyDay = 1;   // D1 default; per-tenant override is a later enhancement.
    private readonly ITenantAdminRepository _tenants;
    private readonly IProvisioningEngine _engine;
    private readonly IPlatformUserRepository _audit;
    private readonly IBranchContext _ctx;
    private readonly IConfiguration _config;

    public OnboardTenantHandler(ITenantAdminRepository tenants, IProvisioningEngine engine, IPlatformUserRepository audit, IBranchContext ctx, IConfiguration config)
    { _tenants = tenants; _engine = engine; _audit = audit; _ctx = ctx; _config = config; }

    public async Task<OnboardTenantResult> Handle(OnboardTenantCommand c, CancellationToken ct)
    {
        if (await _tenants.TenantExistsAsync(c.Code, ct))
            throw new InvalidOperationException($"Tenant '{c.Code}' already exists.");

        var (fyCode, start, end) = FiscalCalc.Build(c.FiscalYearStart, FyMonth, FyDay);

        // Provision the databases FIRST (the failure-prone step). Doing this before writing
        // any platform rows means a provisioning failure leaves no orphaned tenant (L1.5.5).
        // DB creation + schema apply are idempotent, so a retried onboarding reuses them.
        var master = await _engine.ProvisionMasterAsync(c.Code, ct);
        var data = await _engine.ProvisionFiscalYearAsync(c.Code, fyCode, ct);

        var tenantId = await _tenants.CreateTenantAsync(c.Code, c.Name, FyMonth, FyDay, ct);
        var fyId = await _tenants.CreateFiscalYearAsync(tenantId, fyCode, start, end, isCurrent: true, ct);

        await _tenants.AddDomainAsync(tenantId, c.PrimaryHost, isPrimary: true, isCommon: false, ct);
        if (!string.IsNullOrWhiteSpace(c.CommonHost))
            await _tenants.AddDomainAsync(tenantId, c.CommonHost!, isPrimary: false, isCommon: true, ct);

        await _tenants.RegisterDbAsync(tenantId, null, master.DbKind, master.DbName, $"Tenant:{c.Code}:Master", ct);
        await _tenants.RegisterDbAsync(tenantId, fyId, data.DbKind, data.DbName, $"Tenant:{c.Code}:{fyCode}", ct);
        await _tenants.EnableAllModulesAsync(tenantId, fyId, ct);

        // Per-FY billing (L1.4.2): register the subscription + this year's subscription charge.
        var (plan, fee, currency) = BillingDefaults.Read(_config);
        await _tenants.CreateSubscriptionAsync(tenantId, fyId, plan, start, end, ct);
        if (fee != 0)
            await _tenants.WriteBillingLedgerAsync(tenantId, fyId, "Subscription", fee, currency, $"FY {fyCode} subscription ({plan})", ct);

        await _audit.WritePlatformAuditAsync(_ctx.UserId, _ctx.UserName, tenantId,
            "OnboardTenant", "Tenant", c.Code, succeeded: true, error: null, ct);

        return new OnboardTenantResult(tenantId, fyId, fyCode, master.DbName, data.DbName);
    }
}

// ============================ Open next fiscal year (fiscalyear.manage) ============================
/// <summary>Year shift (L1.4.3/L1.5.4): opens a new fiscal year for an existing tenant —
/// provisions its data DB, makes it current, and carries module entitlements forward.</summary>
public sealed record OpenFiscalYearCommand(string TenantCode, int FiscalYearStart) : ICommand<OpenFiscalYearResult>, IAuthorizable
{
    public string RequiredPermission => "fiscalyear.manage";
}
public sealed record OpenFiscalYearResult(int FiscalYearId, string FiscalYearCode, string DataDb);

public sealed class OpenFiscalYearHandler : MediatR.IRequestHandler<OpenFiscalYearCommand, OpenFiscalYearResult>
{
    private const byte FyMonth = 4, FyDay = 1;
    private readonly ITenantAdminRepository _tenants;
    private readonly IProvisioningEngine _engine;
    private readonly IPlatformUserRepository _audit;
    private readonly IBranchContext _ctx;
    private readonly IConfiguration _config;

    public OpenFiscalYearHandler(ITenantAdminRepository tenants, IProvisioningEngine engine, IPlatformUserRepository audit, IBranchContext ctx, IConfiguration config)
    { _tenants = tenants; _engine = engine; _audit = audit; _ctx = ctx; _config = config; }

    public async Task<OpenFiscalYearResult> Handle(OpenFiscalYearCommand c, CancellationToken ct)
    {
        var tenantId = await _tenants.GetTenantIdByCodeAsync(c.TenantCode, ct)
            ?? throw new InvalidOperationException($"Unknown tenant '{c.TenantCode}'.");

        var (fyCode, start, end) = FiscalCalc.Build(c.FiscalYearStart, FyMonth, FyDay);
        if (await _tenants.FiscalYearExistsAsync(tenantId, fyCode, ct))
            throw new InvalidOperationException($"Fiscal year '{fyCode}' already open for '{c.TenantCode}'.");

        // Capture the outgoing current FY BEFORE clearing it — needed to carry entitlements
        // and the billing balance forward (L1.4.3/L1.4.4/L1.4.2).
        var priorFyId = await _tenants.GetCurrentFiscalYearIdAsync(tenantId, ct);

        await _tenants.ClearCurrentFiscalYearAsync(tenantId, ct);
        var fyId = await _tenants.CreateFiscalYearAsync(tenantId, fyCode, start, end, isCurrent: true, ct);

        var data = await _engine.ProvisionFiscalYearAsync(c.TenantCode, fyCode, ct);
        await _tenants.RegisterDbAsync(tenantId, fyId, data.DbKind, data.DbName, $"Tenant:{c.TenantCode}:{fyCode}", ct);

        // Entitlement snapshot (L1.4.4): carry the prior year's per-module entitlements forward
        // (preserving any modules the superadmin had disabled), THEN enable any modules added
        // since (so the new year picks up new platform modules at their default-on state).
        if (priorFyId is int pf) await _tenants.CopyModuleEntitlementsAsync(tenantId, pf, fyId, ct);
        await _tenants.EnableAllModulesAsync(tenantId, fyId, ct);

        // Per-FY billing (L1.4.2): carry the prior-year balance forward, then this year's charge.
        var (plan, fee, currency) = BillingDefaults.Read(_config);
        if (priorFyId is int pb)
        {
            var balance = await _tenants.GetLedgerBalanceAsync(tenantId, pb, ct);
            if (balance != 0)
                await _tenants.WriteBillingLedgerAsync(tenantId, fyId, "CarryForward", balance, currency, "Carried forward from prior fiscal year", ct);
        }
        await _tenants.CreateSubscriptionAsync(tenantId, fyId, plan, start, end, ct);
        if (fee != 0)
            await _tenants.WriteBillingLedgerAsync(tenantId, fyId, "Subscription", fee, currency, $"FY {fyCode} subscription ({plan})", ct);

        await _audit.WritePlatformAuditAsync(_ctx.UserId, _ctx.UserName, tenantId,
            "OpenFiscalYear", "FiscalYear", fyCode, succeeded: true, error: null, ct);

        return new OpenFiscalYearResult(fyId, fyCode, data.DbName);
    }
}

// ============================ List tenants (tenant.manage) ============================
public sealed record TenantRow(int TenantId, string Code, string Name, string? FiscalYear, string DbKind, string DbName);
public sealed record GetTenantsQuery : IQuery<IReadOnlyList<TenantRow>>, IAuthorizable
{
    public string RequiredPermission => "tenant.manage";
}

public sealed class GetTenantsHandler : MediatR.IRequestHandler<GetTenantsQuery, IReadOnlyList<TenantRow>>
{
    private readonly ITenantAdminRepository _tenants;
    public GetTenantsHandler(ITenantAdminRepository tenants) => _tenants = tenants;

    public async Task<IReadOnlyList<TenantRow>> Handle(GetTenantsQuery q, CancellationToken ct)
    {
        var rows = await _tenants.GetTenantsAsync(ct);
        return rows.Select(r => new TenantRow(r.Item1, r.Item2, r.Item3, r.Item4, r.Item5, r.Item6)).ToList();
    }
}

// ============================ Decommission a tenant (tenant.manage) ============================

public sealed record DecommissionTenantResult(string TenantCode, int DatabasesDropped);

/// <summary>
/// Permanently removes a tenant (L1.7): drops its databases, then deletes all control-plane
/// rows + users. Superadmin-only. Requires the caller to re-type the tenant code (Confirm)
/// to guard against accidents. The immutable audit trail is retained.
/// </summary>
public sealed record DecommissionTenantCommand(string TenantCode, string Confirm) : ICommand<DecommissionTenantResult>, IAuthorizable
{
    public string RequiredPermission => "tenant.manage";
}

public sealed class DecommissionTenantValidator : AbstractValidator<DecommissionTenantCommand>
{
    public DecommissionTenantValidator()
    {
        RuleFor(x => x.TenantCode).NotEmpty();
        RuleFor(x => x.Confirm).NotEmpty()
            .Must((cmd, confirm) => string.Equals(confirm, cmd.TenantCode, System.StringComparison.OrdinalIgnoreCase))
            .WithMessage("Type the tenant code to confirm decommission.");
    }
}

public sealed class DecommissionTenantHandler : MediatR.IRequestHandler<DecommissionTenantCommand, DecommissionTenantResult>
{
    private readonly ITenantAdminRepository _tenants;
    private readonly IProvisioningEngine _engine;
    private readonly IPlatformUserRepository _audit;
    private readonly IBranchContext _ctx;
    public DecommissionTenantHandler(ITenantAdminRepository tenants, IProvisioningEngine engine, IPlatformUserRepository audit, IBranchContext ctx)
    { _tenants = tenants; _engine = engine; _audit = audit; _ctx = ctx; }

    public async Task<DecommissionTenantResult> Handle(DecommissionTenantCommand c, CancellationToken ct)
    {
        var tenantId = await _tenants.GetTenantIdByCodeAsync(c.TenantCode, ct)
            ?? throw new InvalidOperationException($"Unknown tenant '{c.TenantCode}'.");

        // Drop the databases first; if a drop fails the platform rows remain, so it is re-runnable.
        var dbs = await _tenants.GetTenantDatabaseNamesAsync(tenantId, ct);
        try
        {
            foreach (var db in dbs) await _engine.DropDatabaseAsync(db, ct);
            await _tenants.DeleteTenantAsync(tenantId, ct);
        }
        catch
        {
            await _audit.WritePlatformAuditAsync(_ctx.UserId, _ctx.UserName, tenantId,
                "DecommissionTenant", "Tenant", c.TenantCode, false, "decommission failed", ct);
            throw;
        }

        await _audit.WritePlatformAuditAsync(_ctx.UserId, _ctx.UserName, tenantId,
            "DecommissionTenant", "Tenant", c.TenantCode, true, null, ct);
        return new DecommissionTenantResult(c.TenantCode, dbs.Count);
    }
}
