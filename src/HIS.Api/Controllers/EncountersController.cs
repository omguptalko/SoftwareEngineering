using HIS.Application.Features.Opd;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace HIS.Api.Controllers;

[ApiController]
[Route("api/encounters")]
public sealed class EncountersController : ControllerBase
{
    private readonly IMediator _mediator;
    public EncountersController(IMediator mediator) => _mediator = mediator;

    /// <summary>Save an OPD consultation (encounter + vitals + diagnoses + prescription).</summary>
    [HttpPost("consultation")]
    public async Task<ActionResult<SaveConsultationResult>> Save([FromBody] SaveConsultationCommand cmd, CancellationToken ct)
    {
        var result = await _mediator.Send(cmd, ct);
        return Ok(result);
    }
}
