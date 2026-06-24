using HIS.Application.Features.Tenant;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace HIS.Api.Controllers;

/// <summary>
/// Tenant-routed data access (L1.6 demonstrator). The serving database is chosen by
/// TenantResolutionMiddleware from the request host / X-Tenant header. Patients are
/// stored in the tenant's master DB; bills in its current fiscal-year DB.
/// </summary>
[ApiController]
[Route("api/tenant")]
public sealed class TenantController : ControllerBase
{
    private readonly IMediator _mediator;
    public TenantController(IMediator mediator) => _mediator = mediator;

    [HttpGet("context")]
    public Task<TenantContextDto> Context(CancellationToken ct) => _mediator.Send(new GetTenantContextQuery(), ct);

    [HttpGet("patients")]
    public Task<IReadOnlyList<TenantPatientRow>> Patients(CancellationToken ct) => _mediator.Send(new GetTenantPatientsQuery(), ct);

    [HttpPost("patients")]
    public Task<AddTenantPatientResult> AddPatient([FromBody] AddTenantPatientCommand cmd, CancellationToken ct) => _mediator.Send(cmd, ct);

    [HttpGet("bills")]
    public Task<IReadOnlyList<TenantBillRow>> Bills(CancellationToken ct) => _mediator.Send(new GetTenantBillsQuery(), ct);

    [HttpPost("bills")]
    public Task<AddTenantBillResult> AddBill([FromBody] AddTenantBillCommand cmd, CancellationToken ct) => _mediator.Send(cmd, ct);
}
