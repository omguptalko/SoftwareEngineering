using HIS.Application.Features.Emergency;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace HIS.Api.Controllers;

[ApiController]
[Route("api/emergency")]
public sealed class EmergencyController : ControllerBase
{
    private readonly IMediator _mediator;
    public EmergencyController(IMediator mediator) => _mediator = mediator;

    /// <summary>Live ED triage board for today, ordered by severity (SRS §3.5).</summary>
    [HttpGet("triage")]
    public Task<IReadOnlyList<TriageBoardRow>> Board(CancellationToken ct) => _mediator.Send(new GetTriageBoardQuery(), ct);

    [HttpPost("triage")]
    public Task<RegisterTriageResult> Triage([FromBody] RegisterTriageCommand cmd, CancellationToken ct) => _mediator.Send(cmd, ct);

    [HttpPost("triage/disposition")]
    public Task<bool> Disposition([FromBody] SetTriageDispositionCommand cmd, CancellationToken ct) => _mediator.Send(cmd, ct);
}
