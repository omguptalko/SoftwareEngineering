using HIS.Api.RealTime;
using HIS.Application.Features.Emergency;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;

namespace HIS.Api.Controllers;

[ApiController]
[Route("api/emergency")]
public sealed class EmergencyController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IHubContext<AlertsHub> _alerts;
    public EmergencyController(IMediator mediator, IHubContext<AlertsHub> alerts) { _mediator = mediator; _alerts = alerts; }

    /// <summary>Live ED triage board for today, ordered by severity (SRS §3.5).</summary>
    [HttpGet("triage")]
    public Task<IReadOnlyList<TriageBoardRow>> Board(CancellationToken ct) => _mediator.Send(new GetTriageBoardQuery(), ct);

    [HttpPost("triage")]
    public async Task<RegisterTriageResult> Triage([FromBody] RegisterTriageCommand cmd, CancellationToken ct)
    {
        var result = await _mediator.Send(cmd, ct);
        // Hospital-wide live alert (task 0.9): every connected board/station is notified.
        await _alerts.Clients.All.SendAsync("emergencyAlert", new
        {
            triageId = result.TriageId,
            category = result.Category,
            isMlc = cmd.IsMlc,
            patient = cmd.PatientUhid,
            status = result.Status
        }, ct);
        return result;
    }

    [HttpPost("triage/disposition")]
    public Task<bool> Disposition([FromBody] SetTriageDispositionCommand cmd, CancellationToken ct) => _mediator.Send(cmd, ct);
}
