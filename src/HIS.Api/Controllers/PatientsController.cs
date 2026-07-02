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

    /// <summary>List this hospital's patients (tenant-scoped). Optional q filters UHID/name/mobile.</summary>
    [HttpGet]
    public Task<IReadOnlyList<PatientListItemDto>> List([FromQuery] string? q, CancellationToken ct) =>
        _mediator.Send(new GetPatientsQuery(q), ct);

    /// <summary>Update a patient's demographics.</summary>
    [HttpPost("update")]
    public Task<bool> Update([FromBody] UpdatePatientCommand cmd, CancellationToken ct) => _mediator.Send(cmd, ct);

    /// <summary>Deactivate / restore a patient (soft delete — keeps clinical history).</summary>
    [HttpPost("set-active")]
    public Task<bool> SetActive([FromBody] SetPatientActiveCommand cmd, CancellationToken ct) => _mediator.Send(cmd, ct);

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
