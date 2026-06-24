using HIS.Application.Features.Patients;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace HIS.Api.Controllers;

[ApiController]
[Route("api/patients")]
public sealed class PatientsController : ControllerBase
{
    private readonly IMediator _mediator;
    public PatientsController(IMediator mediator) => _mediator = mediator;

    /// <summary>Default patient for the workspace banner (replaces HIS.mock.currentPatient).</summary>
    [HttpGet("default")]
    public async Task<ActionResult<PatientDto>> Default(CancellationToken ct)
    {
        var dto = await _mediator.Send(new GetDefaultPatientQuery(), ct);
        return dto is null ? NoContent() : Ok(dto);
    }

    [HttpGet("{uhid}")]
    public async Task<ActionResult<PatientDto>> ByUhid(string uhid, CancellationToken ct)
    {
        var dto = await _mediator.Send(new GetPatientByUhidQuery(uhid), ct);
        return dto is null ? NotFound() : Ok(dto);
    }

    [HttpPost]
    public async Task<ActionResult<RegisterPatientResult>> Register([FromBody] RegisterPatientCommand cmd, CancellationToken ct)
    {
        var result = await _mediator.Send(cmd, ct);
        return CreatedAtAction(nameof(ByUhid), new { uhid = result.Uhid }, result);
    }
}
