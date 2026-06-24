using HIS.Application.Features.Nursing;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace HIS.Api.Controllers;

[ApiController]
[Route("api/nursing")]
public sealed class NursingController : ControllerBase
{
    private readonly IMediator _mediator;
    public NursingController(IMediator mediator) => _mediator = mediator;

    /// <summary>Nursing notes timeline for an admission (SRS §3.13).</summary>
    [HttpGet("admissions/{admissionId:long}/notes")]
    public Task<IReadOnlyList<NursingNoteRow>> Notes(long admissionId, CancellationToken ct)
        => _mediator.Send(new GetNursingNotesQuery(admissionId), ct);

    [HttpPost("notes")]
    public Task<AddNursingNoteResult> AddNote([FromBody] AddNursingNoteCommand cmd, CancellationToken ct)
        => _mediator.Send(cmd, ct);
}
