using HIS.Api.RealTime;
using HIS.Application.Features.Appointments;
using HIS.Application.Features.Opd;
using HIS.Shared.Context;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;

namespace HIS.Api.Controllers;

[ApiController]
[Route("api/appointments")]
public sealed class AppointmentsController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IHubContext<QueueHub> _hub;
    private readonly ITenantContext _tenant;
    public AppointmentsController(IMediator mediator, IHubContext<QueueHub> hub, ITenantContext tenant)
    { _mediator = mediator; _hub = hub; _tenant = tenant; }

    // Push a live signal to the tenant's OPD boards (doctor lobby + waiting-room display).
    private Task NotifyOpd(string action, long appointmentId, CancellationToken ct) =>
        _hub.Clients.Group(TenantGroups.Name(_tenant)).SendAsync("opdChanged", new { action, appointmentId }, ct);

    /// <summary>Today's token queue. Optional doctorCode + status filter (e.g. status=VitalsDone
    /// for a doctor's waiting lobby, or status=InConsultation for the "now calling" board).</summary>
    [HttpGet("queue")]
    public Task<IReadOnlyList<QueueItemDto>> Queue([FromQuery] string? doctorCode, [FromQuery] string? status, CancellationToken ct) =>
        _mediator.Send(new GetTodayQueueQuery(doctorCode, status), ct);

    /// <summary>Vitals station: an attendant records vitals for a booked appointment,
    /// advancing it to 'VitalsDone' (patient enters the doctor's OPD lobby).</summary>
    [HttpPost("{id:long}/vitals")]
    public async Task<bool> RecordVitals(long id, [FromBody] VitalsDto vitals, CancellationToken ct)
    {
        var ok = await _mediator.Send(new RecordVitalsCommand(id, vitals), ct);
        await NotifyOpd("vitals", id, ct);
        return ok;
    }

    /// <summary>Call a waiting (VitalsDone) patient into the consult room -> InConsultation.
    /// Broadcasts to the waiting-room "now calling" board.</summary>
    [HttpPost("{id:long}/call")]
    public async Task<bool> CallNext(long id, CancellationToken ct)
    {
        var ok = await _mediator.Send(new CallNextCommand(id), ct);
        await NotifyOpd("called", id, ct);
        return ok;
    }

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
        await NotifyOpd("booked", result.AppointmentId, ct);
        return Ok(result);
    }
}
