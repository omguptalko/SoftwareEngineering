using FluentValidation;
using HIS.Application.Abstractions;
using HIS.Domain.Entities;
using HIS.Shared.Context;

namespace HIS.Application.Features.Appointments;

/// <summary>Books a doctor appointment and issues a token — SRS §3.2.
/// Accepts the codes/UHID the UI already holds (from F3 lookups); resolves IDs server-side.</summary>
public sealed record BookAppointmentCommand(
    string DoctorCode, string? PatientUhid, string? Department,
    DateTime SlotStart, string? VisitType, string? Mode)
    : ICommand<BookAppointmentResult>, IAuditable
{
    public string AuditEntity => "Appointment";
    public string? AuditEntityId => DoctorCode;
}

public sealed record BookAppointmentResult(long AppointmentId, string TokenNo);

public sealed class BookAppointmentValidator : AbstractValidator<BookAppointmentCommand>
{
    public BookAppointmentValidator()
    {
        RuleFor(x => x.DoctorCode).NotEmpty();
        RuleFor(x => x.SlotStart).Must(d => d != default).WithMessage("A slot is required.");
    }
}

public sealed class BookAppointmentHandler : MediatR.IRequestHandler<BookAppointmentCommand, BookAppointmentResult>
{
    private readonly IAppointmentRepository _appts;
    private readonly IPatientRepository _patients;
    private readonly IBranchContext _ctx;

    public BookAppointmentHandler(IAppointmentRepository appts, IPatientRepository patients, IBranchContext ctx)
    {
        _appts = appts; _patients = patients; _ctx = ctx;
    }

    public async Task<BookAppointmentResult> Handle(BookAppointmentCommand c, CancellationToken ct)
    {
        var branchId = _ctx.BranchId ?? throw new InvalidOperationException("Branch context required.");

        var doctorId = await _appts.GetDoctorIdByCodeAsync(LookupCode.Parse(c.DoctorCode), ct)
            ?? throw new InvalidOperationException($"Unknown doctor '{c.DoctorCode}'.");

        long? patientId = null;
        if (!string.IsNullOrWhiteSpace(c.PatientUhid))
        {
            var p = await _patients.GetByUhidAsync(LookupCode.Parse(c.PatientUhid!), ct);
            patientId = p?.PatientId;
        }

        var token = await _appts.NextTokenAsync(branchId, doctorId, c.SlotStart.Date, ct);

        var id = await _appts.InsertAsync(new Appointment
        {
            BranchId = branchId,
            PatientId = patientId,
            DoctorId = doctorId,
            Department = c.Department,
            SlotStart = c.SlotStart,
            VisitType = c.VisitType,
            Mode = c.Mode,
            TokenNo = token,
            Status = "Booked",
            CreatedUtc = DateTime.UtcNow
        }, ct);

        return new BookAppointmentResult(id, token);
    }
}

/// <summary>The F3 lookups fill fields as "CODE — Label"; extract the code part.</summary>
public static class LookupCode
{
    public static string Parse(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return value;
        var idx = value.IndexOf('—');                 // em-dash used by the UI
        if (idx < 0) idx = value.IndexOf(" - ", StringComparison.Ordinal);
        return (idx > 0 ? value[..idx] : value).Trim();
    }

    /// <summary>Returns the label tail (e.g. "ICU — ICU-03" → "ICU-03"), for ward/bed pickers.</summary>
    public static string ParseTail(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return value;
        var idx = value.LastIndexOf('—');
        return (idx >= 0 ? value[(idx + 1)..] : value).Trim();
    }
}
