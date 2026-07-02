using HIS.Application.Abstractions;
using HIS.Shared.Context;
using Microsoft.Extensions.Configuration;

namespace HIS.Application.Features.Appointments;

// ---- Today's queue (SRS §3.2) — carries AppointmentId/UHID/vitals so the OPD
//      doctor lobby can select a booked patient and consult (no more static UHID).
//      Optional Status filter drives the lobby (e.g. Status='VitalsDone'). ----
public sealed record QueueItemDto(long AppointmentId, string Token, string Uhid, string Patient, string Doctor, string Status, bool HasVitals);

public sealed record GetTodayQueueQuery(string? DoctorCode, string? Status = null) : IQuery<IReadOnlyList<QueueItemDto>>;

public sealed class GetTodayQueueHandler : MediatR.IRequestHandler<GetTodayQueueQuery, IReadOnlyList<QueueItemDto>>
{
    private readonly IAppointmentRepository _appts;
    private readonly IBranchContext _ctx;

    public GetTodayQueueHandler(IAppointmentRepository appts, IBranchContext ctx) { _appts = appts; _ctx = ctx; }

    public async Task<IReadOnlyList<QueueItemDto>> Handle(GetTodayQueueQuery q, CancellationToken ct)
    {
        var branchId = _ctx.BranchId ?? 0;
        int? doctorId = null;
        if (!string.IsNullOrWhiteSpace(q.DoctorCode))
            doctorId = await _appts.GetDoctorIdByCodeAsync(LookupCode.Parse(q.DoctorCode!), ct);

        var rows = await _appts.GetTodayQueueAsync(branchId, doctorId, DateTime.Now.Date, q.Status, ct);
        return rows.Select(r => new QueueItemDto(r.AppointmentId, r.TokenNo, r.Uhid, r.PatientName, r.DoctorName, r.Status, r.HasVitals)).ToList();
    }
}

// ---- Upcoming appointments (future-dated) — surfaces follow-ups booked from a
//      consult, which the date-scoped Today's Queue never shows. ----
public sealed record UpcomingApptDto(long AppointmentId, DateTime SlotStart, string Token, string Uhid, string Patient, string Doctor, string Department, string VisitType, string Status);

public sealed record GetUpcomingApptsQuery(string? DoctorCode = null, bool FollowUpOnly = false) : IQuery<IReadOnlyList<UpcomingApptDto>>;

public sealed class GetUpcomingApptsHandler : MediatR.IRequestHandler<GetUpcomingApptsQuery, IReadOnlyList<UpcomingApptDto>>
{
    private readonly IAppointmentRepository _appts;
    private readonly IBranchContext _ctx;

    public GetUpcomingApptsHandler(IAppointmentRepository appts, IBranchContext ctx) { _appts = appts; _ctx = ctx; }

    public async Task<IReadOnlyList<UpcomingApptDto>> Handle(GetUpcomingApptsQuery q, CancellationToken ct)
    {
        var branchId = _ctx.BranchId ?? 0;
        int? doctorId = null;
        if (!string.IsNullOrWhiteSpace(q.DoctorCode))
            doctorId = await _appts.GetDoctorIdByCodeAsync(LookupCode.Parse(q.DoctorCode!), ct);

        // From tomorrow onward: today's appointments already appear in the Today's Queue.
        var rows = await _appts.GetUpcomingAsync(branchId, DateTime.Now.Date.AddDays(1), doctorId, q.FollowUpOnly, ct);
        return rows.Select(r => new UpcomingApptDto(r.AppointmentId, r.SlotStart, r.TokenNo, r.Uhid, r.PatientName, r.DoctorName, r.Department, r.VisitType, r.Status)).ToList();
    }
}

// ---- Doctor slots for a date (working hours from config, never hardcoded) ----
public sealed record SlotDto(string Time, DateTime SlotStart, bool Booked);

public sealed record GetDoctorSlotsQuery(string DoctorCode, DateTime Date) : IQuery<IReadOnlyList<SlotDto>>;

public sealed class GetDoctorSlotsHandler : MediatR.IRequestHandler<GetDoctorSlotsQuery, IReadOnlyList<SlotDto>>
{
    private readonly IAppointmentRepository _appts;
    private readonly IConfiguration _config;

    public GetDoctorSlotsHandler(IAppointmentRepository appts, IConfiguration config) { _appts = appts; _config = config; }

    public async Task<IReadOnlyList<SlotDto>> Handle(GetDoctorSlotsQuery q, CancellationToken ct)
    {
        // Working-hours template is configuration, not hardcoded business logic.
        var startHour = _config.GetValue("Scheduling:SlotStartHour", 9);
        var endHour = _config.GetValue("Scheduling:SlotEndHour", 17);
        var stepMin = _config.GetValue("Scheduling:SlotMinutes", 30);

        var doctorId = await _appts.GetDoctorIdByCodeAsync(LookupCode.Parse(q.DoctorCode), ct);
        if (doctorId is null) return Array.Empty<SlotDto>();

        var booked = (await _appts.GetBookedSlotStartsAsync(doctorId.Value, q.Date.Date, ct))
            .Select(d => d.TimeOfDay).ToHashSet();

        var slots = new List<SlotDto>();
        for (var t = new TimeSpan(startHour, 0, 0); t < new TimeSpan(endHour, 0, 0); t += TimeSpan.FromMinutes(stepMin))
        {
            var start = q.Date.Date + t;
            slots.Add(new SlotDto($"{t.Hours:D2}:{t.Minutes:D2}", start, booked.Contains(t)));
        }
        return slots;
    }
}
