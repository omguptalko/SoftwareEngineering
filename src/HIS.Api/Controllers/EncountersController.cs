using HIS.Api.RealTime;
using HIS.Application.Features.Opd;
using HIS.Shared.Context;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;

namespace HIS.Api.Controllers;

[ApiController]
[Route("api/encounters")]
public sealed class EncountersController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IHubContext<QueueHub> _hub;
    private readonly ITenantContext _tenant;
    public EncountersController(IMediator mediator, IHubContext<QueueHub> hub, ITenantContext tenant)
    { _mediator = mediator; _hub = hub; _tenant = tenant; }

    /// <summary>Save an OPD consultation (encounter + vitals + diagnoses + prescription).
    /// When it closes a queued appointment, pushes a live OPD-board update.</summary>
    [HttpPost("consultation")]
    public async Task<ActionResult<SaveConsultationResult>> Save([FromBody] SaveConsultationCommand cmd, CancellationToken ct)
    {
        var result = await _mediator.Send(cmd, ct);
        if (cmd.AppointmentId is long apptId)
            await _hub.Clients.Group(TenantGroups.Name(_tenant)).SendAsync("opdChanged", new { action = "completed", appointmentId = apptId }, ct);
        return Ok(result);
    }

    /// <summary>The structured department-template answers recorded on an encounter.</summary>
    [HttpGet("{id:long}/template-data")]
    public Task<IReadOnlyList<TemplateAnswerDto>> TemplateData(long id, CancellationToken ct) =>
        _mediator.Send(new GetEncounterTemplateDataQuery(id), ct);
}
