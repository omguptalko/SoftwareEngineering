using HIS.Application.Features.Ot;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace HIS.Api.Controllers;

[ApiController]
[Route("api/ot")]
public sealed class OtController : ControllerBase
{
    private readonly IMediator _mediator;
    public OtController(IMediator mediator) => _mediator = mediator;

    /// <summary>Operation-theatre board (scheduled + completed cases) — SRS §3.12.</summary>
    [HttpGet("board")]
    public Task<IReadOnlyList<OtBoardRow>> Board(CancellationToken ct) => _mediator.Send(new GetOtBoardQuery(), ct);

    [HttpPost("schedule")]
    public Task<ScheduleSurgeryResult> Schedule([FromBody] ScheduleSurgeryCommand cmd, CancellationToken ct) => _mediator.Send(cmd, ct);

    /// <summary>Wheel-in: move a scheduled case to In Progress.</summary>
    [HttpPost("start")]
    public Task<bool> Start([FromBody] StartSurgeryCommand cmd, CancellationToken ct) => _mediator.Send(cmd, ct);

    [HttpPost("complete")]
    public Task<bool> Complete([FromBody] CompleteSurgeryCommand cmd, CancellationToken ct) => _mediator.Send(cmd, ct);
}
