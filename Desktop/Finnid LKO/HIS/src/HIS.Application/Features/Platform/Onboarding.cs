using FluentValidation;
using HIS.Application.Abstractions;
using HIS.Shared.Context;

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

    public OnboardTenantHandler(ITenantAdminRepository tenants, IProvisioningEngine engine, IPlatformUserRepository audit, IBranchContext ctx)
    { _tenants = tenants; _engine = engine; _audit = audit; _ctx = ctx; }

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

    public OpenFiscalYearHandler(ITenantAdminRepository tenants, IProvisioningEngine engine, IPlatformUserRepository audit, IBranchContext ctx)
    { _tenants = tenants; _engine = engine; _audit = audit; _ctx = ctx; }

    public async Task<OpenFiscalYearResult> Handle(OpenFiscalYearCommand c, CancellationToken ct)
    {
        var tenantId = await _tenants.GetTenantIdByCodeAsync(c.TenantCode, ct)
            ?? throw new InvalidOperationException($"Unknown tenant '{c.TenantCode}'.");

        var (fyCode, start, end) = FiscalCalc.Build(c.FiscalYearStart, FyMonth, FyDay);
        if (await _tenants.FiscalYearExistsAsync(tenantId, fyCode, ct))
            throw new InvalidOperationException($"Fiscal year '{fyCode}' already open for '{c.TenantCode}'.");

        await _tenants.ClearCurrentFiscalYearAsync(tenantId, ct);
        var fyId = await _tenants.CreateFiscalYearAsync(tenantId, fyCode, start, end, isCurrent: true, ct);

        var data = await _engine.ProvisionFiscalYearAsync(c.TenantCode, fyCode, ct);
        await _tenants.RegisterDbAsync(tenantId, fyId, data.DbKind, data.DbName, $"Tenant:{c.TenantCode}:{fyCode}", ct);
        await _tenants.EnableAllModulesAsync(tenantId, fyId, ct);

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
