using HIS.Application.Features.Platform;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace HIS.Api.Controllers;

[ApiController]
[Route("api/platform")]
public sealed class PlatformController : ControllerBase
{
    private readonly IMediator _mediator;
    public PlatformController(IMediator mediator) => _mediator = mediator;

    /// <summary>Control-plane audit trail. RBAC-gated by 'audit.read' (L1.2.6).</summary>
    [HttpGet("audit")]
    public Task<IReadOnlyList<PlatformAuditRow>> Audit([FromQuery] int take = 50, CancellationToken ct = default)
        => _mediator.Send(new GetPlatformAuditQuery(take), ct);

    // ---- Dynamic module/page registry + assignments (L1.3) ----

    /// <summary>Create a module (gated by 'module.manage').</summary>
    [HttpPost("modules")]
    public Task<int> CreateModule([FromBody] CreateModuleCommand cmd, CancellationToken ct) => _mediator.Send(cmd, ct);

    /// <summary>Create a page within a module (gated by 'module.manage').</summary>
    [HttpPost("pages")]
    public Task<int> CreatePage([FromBody] CreatePageCommand cmd, CancellationToken ct) => _mediator.Send(cmd, ct);

    /// <summary>Assign a module to a role (gated by 'rbac.manage').</summary>
    [HttpPost("assign/module")]
    public Task<bool> AssignModule([FromBody] AssignModuleToRoleCommand cmd, CancellationToken ct) => _mediator.Send(cmd, ct);

    /// <summary>Assign a page to a role (gated by 'rbac.manage').</summary>
    [HttpPost("assign/page")]
    public Task<bool> AssignPage([FromBody] AssignPageToRoleCommand cmd, CancellationToken ct) => _mediator.Send(cmd, ct);

    // ---- Onboarding + provisioning (L1.5/L1.7) ----

    /// <summary>List onboarded tenants and their databases (gated by 'tenant.manage').</summary>
    [HttpGet("tenants")]
    public Task<IReadOnlyList<TenantRow>> Tenants(CancellationToken ct) => _mediator.Send(new GetTenantsQuery(), ct);

    /// <summary>Onboard a hospital: registers tenant/fiscal-year/domains and auto-provisions
    /// its master + fiscal-year databases (gated by 'tenant.onboard').</summary>
    [HttpPost("tenants/onboard")]
    public Task<OnboardTenantResult> Onboard([FromBody] OnboardTenantCommand cmd, CancellationToken ct) => _mediator.Send(cmd, ct);

    /// <summary>Open a new fiscal year for a tenant (year shift) — provisions its data DB
    /// (gated by 'fiscalyear.manage').</summary>
    [HttpPost("fiscal-years/open")]
    public Task<OpenFiscalYearResult> OpenFiscalYear([FromBody] OpenFiscalYearCommand cmd, CancellationToken ct) => _mediator.Send(cmd, ct);
}
