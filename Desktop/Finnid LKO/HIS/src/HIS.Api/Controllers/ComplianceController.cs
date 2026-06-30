using HIS.Application.Features.Compliance;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace HIS.Api.Controllers;

/// <summary>Compliance &amp; Audit (SRS §3.22, Phase 12.2) — view the immutable audit trail.</summary>
[ApiController]
[Route("api/audit")]
public sealed class ComplianceController : ControllerBase
{
    private readonly IMediator _mediator;
    public ComplianceController(IMediator mediator) => _mediator = mediator;

    /// <summary>Recent audit-trail entries for the resolved tenant (auth required).</summary>
    [HttpGet]
    public Task<IReadOnlyList<AuditTrailRow>> Trail([FromQuery] int take = 100, CancellationToken ct = default)
        => _mediator.Send(new GetAuditTrailQuery(take), ct);
}
