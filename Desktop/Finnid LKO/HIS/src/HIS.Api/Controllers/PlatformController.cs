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
}
