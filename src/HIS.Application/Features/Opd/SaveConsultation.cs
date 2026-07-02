using FluentValidation;
using HIS.Application.Abstractions;
using HIS.Application.Features.Appointments;   // LookupCode
using HIS.Domain.Entities;
using HIS.Shared.Context;

namespace HIS.Application.Features.Opd;

public sealed record VitalsDto(
    decimal? TempF, int? Pulse, int? BpSystolic, int? BpDiastolic,
    int? Spo2, int? RespRate, decimal? WeightKg, decimal? HeightCm, int? Grbs);

public sealed record RxLineDto(string DrugCode, string? Dose, string? Frequency, int? Days, string? Route, int? Qty);

/// <summary>A structured answer to a department-template field (persisted on the encounter).</summary>
public sealed record TemplateAnswerDto(string Label, string? FieldType, string? Value);

/// <summary>Persists an OPD consultation in one unit: encounter + vitals + diagnoses +
/// prescription + follow-up/advice (SRS §3.3). Codes/UHID resolved server-side.</summary>
public sealed record SaveConsultationCommand(
    string PatientUhid, string DoctorCode,
    VitalsDto? Vitals, string? Complaints, string? History, string? Advice, DateTime? FollowUpDate,
    IReadOnlyList<string>? DiagnosisCodes, IReadOnlyList<RxLineDto>? Prescription,
    IReadOnlyList<string>? LabTests = null,   // investigations ordered -> raised in the Lab worklist
    long? AppointmentId = null,   // when consulting a queued patient: links vitals + closes the token
    string? Department = null,    // OPD specialty
    IReadOnlyList<TemplateAnswerDto>? TemplateAnswers = null)   // structured department-template answers
    : ICommand<SaveConsultationResult>, IAuditable, IAuthorizable
{
    public string AuditEntity => "Encounter";
    public string? AuditEntityId => PatientUhid;
    public string RequiredPermission => "opd.consult";    // doctor/admin only
}

public sealed record SaveConsultationResult(
    long EncounterId, long? PrescriptionId,
    long? FollowUpAppointmentId = null, string? FollowUpToken = null, DateTime? FollowUpDate = null);

public sealed class SaveConsultationValidator : AbstractValidator<SaveConsultationCommand>
{
    public SaveConsultationValidator()
    {
        RuleFor(x => x.PatientUhid).NotEmpty();
        RuleFor(x => x.DoctorCode).NotEmpty();
    }
}

public sealed class SaveConsultationHandler : MediatR.IRequestHandler<SaveConsultationCommand, SaveConsultationResult>
{
    private readonly IEncounterRepository _enc;
    private readonly IPatientRepository _patients;
    private readonly IAppointmentRepository _appts;
    private readonly MediatR.ISender _sender;
    private readonly IBranchContext _ctx;

    public SaveConsultationHandler(IEncounterRepository enc, IPatientRepository patients, IAppointmentRepository appts, MediatR.ISender sender, IBranchContext ctx)
    {
        _enc = enc; _patients = patients; _appts = appts; _sender = sender; _ctx = ctx;
    }

    public async Task<SaveConsultationResult> Handle(SaveConsultationCommand c, CancellationToken ct)
    {
        var branchId = _ctx.BranchId ?? throw new InvalidOperationException("Branch context required.");

        var patient = await _patients.GetByUhidAsync(LookupCode.Parse(c.PatientUhid), ct)
            ?? throw new InvalidOperationException($"Unknown patient '{c.PatientUhid}'.");
        var doctorId = await _enc.GetDoctorIdByCodeAsync(LookupCode.Parse(c.DoctorCode), ct);

        var encounterId = await _enc.CreateEncounterAsync(new Encounter
        {
            BranchId = branchId,
            PatientId = patient.PatientId,
            DoctorId = doctorId,
            EncType = "OPD",
            StartedUtc = DateTime.UtcNow,
            Complaints = c.Complaints,
            History = c.History,
            Advice = c.Advice,
            FollowUpDate = c.FollowUpDate,
            Status = "Closed"
        }, ct);

        if (c.Vitals is { } v && HasAnyVital(v))
        {
            await _enc.SaveVitalsAsync(new Vitals
            {
                EncounterId = encounterId,
                RecordedUtc = DateTime.UtcNow,
                TempF = v.TempF, Pulse = v.Pulse, BpSystolic = v.BpSystolic, BpDiastolic = v.BpDiastolic,
                Spo2 = v.Spo2, RespRate = v.RespRate, WeightKg = v.WeightKg, HeightCm = v.HeightCm, Grbs = v.Grbs
            }, ct);
        }

        // OPD lobby flow: attach the station-recorded vitals to this encounter and close the token.
        if (c.AppointmentId is long apptId)
        {
            await _enc.LinkApptVitalsAsync(apptId, encounterId, ct);
            await _appts.SetStatusAsync(apptId, "Completed", ct);
        }

        // Structured department-template answers (queryable, not folded into free-text history).
        if (c.TemplateAnswers is { Count: > 0 })
            await _enc.SaveTemplateAnswersAsync(encounterId, c.Department,
                c.TemplateAnswers.Select(a => (a.Label, a.FieldType, a.Value)).ToList(), ct);

        if (c.DiagnosisCodes is { Count: > 0 })
            foreach (var dx in c.DiagnosisCodes.Where(d => !string.IsNullOrWhiteSpace(d)))
                await _enc.AddDiagnosisAsync(encounterId, LookupCode.Parse(dx), true, ct);

        // Downstream: raise ordered investigations in the Lab worklist (reuses the LIS feature).
        if (c.LabTests is { Count: > 0 })
            foreach (var test in c.LabTests.Where(t => !string.IsNullOrWhiteSpace(t)))
                await _sender.Send(new HIS.Application.Features.Diagnostics.CreateLabOrderCommand(c.PatientUhid, test), ct);

        long? prescriptionId = null;
        var lines = (c.Prescription ?? Array.Empty<RxLineDto>()).Where(l => !string.IsNullOrWhiteSpace(l.DrugCode)).ToList();
        if (lines.Count > 0)
        {
            prescriptionId = await _enc.CreatePrescriptionAsync(encounterId, ct);
            foreach (var l in lines)
            {
                var drugId = await _enc.GetDrugIdByCodeAsync(LookupCode.Parse(l.DrugCode), ct);
                await _enc.AddPrescriptionLineAsync(new PrescriptionLine
                {
                    PrescriptionId = prescriptionId.Value,
                    DrugId = drugId,
                    Dose = l.Dose, Frequency = l.Frequency, Days = l.Days, Route = l.Route, Qty = l.Qty
                }, ct);
            }
        }

        // Follow-up scheduling: if the doctor picked a next-visit date, issue a Follow-up
        // appointment + token straight away (same token scheme as normal booking). SRS 3.2.
        long? followUpApptId = null; string? followUpToken = null; DateTime? followUpOn = null;
        if (c.FollowUpDate is { } fu && fu.Date >= DateTime.UtcNow.Date && doctorId is int fuDoctorId)
        {
            followUpOn = fu.Date;
            followUpToken = await _appts.NextTokenAsync(branchId, fuDoctorId, fu.Date, ct);
            followUpApptId = await _appts.InsertAsync(new Appointment
            {
                BranchId = branchId,
                PatientId = patient.PatientId,
                DoctorId = fuDoctorId,
                Department = c.Department,
                SlotStart = fu.Date.AddHours(9),   // default morning OPD slot
                VisitType = "Follow-up",
                Mode = "Walk-in",
                TokenNo = followUpToken,
                Status = "Booked",
                CreatedUtc = DateTime.UtcNow
            }, ct);
        }

        return new SaveConsultationResult(encounterId, prescriptionId, followUpApptId, followUpToken, followUpOn);
    }

    private static bool HasAnyVital(VitalsDto v) =>
        v.TempF.HasValue || v.Pulse.HasValue || v.BpSystolic.HasValue || v.BpDiastolic.HasValue ||
        v.Spo2.HasValue || v.RespRate.HasValue || v.WeightKg.HasValue || v.HeightCm.HasValue || v.Grbs.HasValue;
}
