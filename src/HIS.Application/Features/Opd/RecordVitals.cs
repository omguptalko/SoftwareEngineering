using FluentValidation;
using HIS.Application.Abstractions;
using HIS.Domain.Entities;

namespace HIS.Application.Features.Opd;

/// <summary>
/// Vitals station (SRS §3.2a). A vitals-taking attendant records vitals for a BOOKED
/// appointment — a step BEFORE the doctor's consultation. Recording vitals advances the
/// appointment to 'VitalsDone', which places the patient in the doctor's OPD waiting lobby.
/// This separates the attendant's task from the doctor's, and drives the queue.
/// </summary>
public sealed record RecordVitalsCommand(long AppointmentId, VitalsDto Vitals) : ICommand<bool>, IAuditable
{
    public string AuditEntity => "Appointment";
    public string? AuditEntityId => AppointmentId.ToString();
}

public sealed class RecordVitalsValidator : AbstractValidator<RecordVitalsCommand>
{
    public RecordVitalsValidator()
    {
        RuleFor(x => x.AppointmentId).GreaterThan(0);
        RuleFor(x => x.Vitals).NotNull();
    }
}

public sealed class RecordVitalsHandler : MediatR.IRequestHandler<RecordVitalsCommand, bool>
{
    private readonly IAppointmentRepository _appts;
    private readonly IEncounterRepository _enc;
    public RecordVitalsHandler(IAppointmentRepository appts, IEncounterRepository enc) { _appts = appts; _enc = enc; }

    public async Task<bool> Handle(RecordVitalsCommand c, CancellationToken ct)
    {
        var appt = await _appts.GetAppointmentAsync(c.AppointmentId, ct)
            ?? throw new InvalidOperationException($"Appointment {c.AppointmentId} not found.");
        if (string.Equals(appt.Status, "Completed", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("This appointment's consultation is already completed.");

        var v = c.Vitals;
        await _enc.SaveApptVitalsAsync(c.AppointmentId, new Vitals
        {
            RecordedUtc = DateTime.UtcNow,
            TempF = v.TempF, Pulse = v.Pulse, BpSystolic = v.BpSystolic, BpDiastolic = v.BpDiastolic,
            Spo2 = v.Spo2, RespRate = v.RespRate, WeightKg = v.WeightKg, HeightCm = v.HeightCm, Grbs = v.Grbs
        }, ct);
        await _appts.SetStatusAsync(c.AppointmentId, "VitalsDone", ct);   // -> doctor's OPD lobby
        return true;
    }
}

/// <summary>Call a waiting (VitalsDone) patient into the consult room. Advances the appointment
/// to 'InConsultation' and stamps CalledUtc — used by the waiting-room "now calling" display.</summary>
public sealed record CallNextCommand(long AppointmentId) : ICommand<bool>, IAuditable
{
    public string AuditEntity => "Appointment";
    public string? AuditEntityId => AppointmentId.ToString();
}

public sealed class CallNextHandler : MediatR.IRequestHandler<CallNextCommand, bool>
{
    private readonly IAppointmentRepository _appts;
    public CallNextHandler(IAppointmentRepository appts) { _appts = appts; }
    public async Task<bool> Handle(CallNextCommand c, CancellationToken ct)
    {
        var appt = await _appts.GetAppointmentAsync(c.AppointmentId, ct)
            ?? throw new InvalidOperationException($"Appointment {c.AppointmentId} not found.");
        if (string.Equals(appt.Status, "Completed", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("This appointment is already completed.");
        if (!string.Equals(appt.Status, "VitalsDone", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(appt.Status, "InConsultation", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Vitals must be recorded before the patient can be called in.");
        await _appts.MarkCalledAsync(c.AppointmentId, ct);
        return true;
    }
}

/// <summary>The station-recorded vitals for an appointment, for the doctor's read-only preload.</summary>
public sealed record GetApptVitalsQuery(long AppointmentId) : IQuery<VitalsDto?>;

public sealed class GetApptVitalsHandler : MediatR.IRequestHandler<GetApptVitalsQuery, VitalsDto?>
{
    private readonly IEncounterRepository _enc;
    public GetApptVitalsHandler(IEncounterRepository enc) { _enc = enc; }
    public async Task<VitalsDto?> Handle(GetApptVitalsQuery q, CancellationToken ct)
    {
        var v = await _enc.GetApptVitalsAsync(q.AppointmentId, ct);
        return v is null ? null
            : new VitalsDto(v.TempF, v.Pulse, v.BpSystolic, v.BpDiastolic, v.Spo2, v.RespRate, v.WeightKg, v.HeightCm, v.Grbs);
    }
}
