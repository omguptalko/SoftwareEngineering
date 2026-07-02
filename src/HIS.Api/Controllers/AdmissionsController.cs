using HIS.Application.Features.Ipd;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace HIS.Api.Controllers;

[ApiController]
[Route("api/ipd")]
public sealed class AdmissionsController : ControllerBase
{
    private readonly IMediator _mediator;
    public AdmissionsController(IMediator mediator) => _mediator = mediator;

    /// <summary>Live bed board with occupants (SRS §3.4).</summary>
    [HttpGet("bedboard")]
    public Task<IReadOnlyList<BedDto>> BedBoard(CancellationToken ct) => _mediator.Send(new GetBedBoardQuery(), ct);

    /// <summary>Currently-admitted patients — who is in which ward/bed (SRS §3.4).</summary>
    [HttpGet("admissions")]
    public Task<IReadOnlyList<AdmittedPatientDto>> Admissions(CancellationToken ct) => _mediator.Send(new GetAdmittedPatientsQuery(), ct);

    [HttpPost("admit")]
    public Task<AdmitPatientResult> Admit([FromBody] AdmitPatientCommand cmd, CancellationToken ct) => _mediator.Send(cmd, ct);

    [HttpPost("transfer")]
    public Task<bool> Transfer([FromBody] TransferBedCommand cmd, CancellationToken ct) => _mediator.Send(cmd, ct);

    [HttpPost("discharge")]
    public Task<bool> Discharge([FromBody] DischargePatientCommand cmd, CancellationToken ct) => _mediator.Send(cmd, ct);

    /// <summary>Housekeeping: return a cleaned bed to the available pool (clean -> free),
    /// so beds recycle after discharge (SRS §3.4).</summary>
    [HttpPost("beds/{bedNo}/ready")]
    public Task<bool> MarkBedReady(string bedNo, CancellationToken ct) => _mediator.Send(new MarkBedReadyCommand(bedNo), ct);
}
