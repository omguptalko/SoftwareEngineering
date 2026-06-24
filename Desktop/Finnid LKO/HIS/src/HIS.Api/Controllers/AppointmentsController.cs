using HIS.Application.Features.Appointments;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace HIS.Api.Controllers;

[ApiController]
[Route("api/appointments")]
public sealed class AppointmentsController : ControllerBase
{
    private readonly IMediator _mediator;
    public AppointmentsController(IMediator mediator) => _mediator = mediator;

    /// <summary>Today's token queue (optionally filtered by doctor).</summary>
    [HttpGet("queue")]
    public Task<IReadOnlyList<QueueItemDto>> Queue([FromQuery] string? doctorCode, CancellationToken ct) =>
        _mediator.Send(new GetTodayQueueQuery(doctorCode), ct);

    /// <summary>Doctor slots for a date (working hours from config).</summary>
    [HttpGet("slots")]
    public Task<IReadOnlyList<SlotDto>> Slots([FromQuery] string doctorCode, [FromQuery] DateTime date, CancellationToken ct) =>
        _mediator.Send(new GetDoctorSlotsQuery(doctorCode, date), ct);

    /// <summary>Book an appointment and issue a token.</summary>
    [HttpPost]
    public async Task<ActionResult<BookAppointmentResult>> Book([FromBody] BookAppointmentCommand cmd, CancellationToken ct)
    {
        var result = await _mediator.Send(cmd, ct);
        return Ok(result);
    }
}
