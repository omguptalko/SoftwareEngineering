using HIS.Application.Features.Appointments;
using HIS.Application.Features.Opd;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace HIS.Api.Controllers;

[ApiController]
[Route("api/appointments")]
public sealed class AppointmentsController : ControllerBase
{
    private readonly IMediator _mediator;
    public AppointmentsController(IMediator mediator) => _mediator = mediator;

    /// <summary>Today's token queue. Optional doctorCode + status filter (e.g. status=VitalsDone
    /// for a doctor's waiting lobby).</summary>
    [HttpGet("queue")]
    public Task<IReadOnlyList<QueueItemDto>> Queue([FromQuery] string? doctorCode, [FromQuery] string? status, CancellationToken ct) =>
        _mediator.Send(new GetTodayQueueQuery(doctorCode, status), ct);

    /// <summary>Vitals station: an attendant records vitals for a booked appointment,
    /// advancing it to 'VitalsDone' (patient enters the doctor's OPD lobby).</summary>
    [HttpPost("{id:long}/vitals")]
    public Task<bool> RecordVitals(long id, [FromBody] VitalsDto vitals, CancellationToken ct) =>
        _mediator.Send(new RecordVitalsCommand(id, vitals), ct);

    /// <summary>The station-recorded vitals for an appointment (doctor's read-only preload).</summary>
    [HttpGet("{id:long}/vitals")]
    public Task<VitalsDto?> ApptVitals(long id, CancellationToken ct) =>
        _mediator.Send(new GetApptVitalsQuery(id), ct);

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
