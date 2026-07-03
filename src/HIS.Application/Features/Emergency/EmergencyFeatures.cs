using FluentValidation;
using HIS.Application.Abstractions;
using HIS.Application.Features.Appointments;   // LookupCode
using HIS.Application.Features.Ipd;            // AdmitPatientCommand
using HIS.Domain.Entities;
using HIS.Shared.Context;
using Microsoft.Extensions.Configuration;

namespace HIS.Application.Features.Emergency;

/// <summary>5-level colour acuity mapped to a level (1=Resuscitation .. 5=Non-urgent). SRS §3.5.</summary>
internal static class TriageAcuity
{
    public static byte? LevelForColour(string? colour) => (colour ?? "").Trim().ToLowerInvariant() switch
    {
        "red" => 1, "orange" => 2, "yellow" => 3, "green" => 4, "blue" => 5, _ => null
    };
}

// ============================ Triage register (§3.5) ============================
/// <summary>Triage an emergency arrival — 5-level colour acuity + triage vitals/GCS + presenting
/// picture. Category validated against config (Emergency:TriageCategories). Patient optional
/// (unknown/unconscious arrivals allowed).</summary>
public sealed record RegisterTriageCommand(
    string? PatientUhid, string Category, bool IsMlc, string? Notes,
    string? ChiefComplaint = null, string? ArrivalMode = null, byte? TriageLevel = null,
    byte? PainScore = null, byte? GcsTotal = null,
    decimal? TempF = null, int? Pulse = null, int? BpSystolic = null, int? BpDiastolic = null,
    int? Spo2 = null, int? RespRate = null, int? Grbs = null, string? AttendingDoctorCode = null)
    : ICommand<RegisterTriageResult>, IAuditable
{
    public string AuditEntity => "EmergencyTriage";
    public string? AuditEntityId => PatientUhid;
}
public sealed record RegisterTriageResult(long TriageId, string Category, byte? TriageLevel, string Status);

public sealed class RegisterTriageValidator : AbstractValidator<RegisterTriageCommand>
{
    public RegisterTriageValidator() => RuleFor(x => x.Category).NotEmpty();
}

public sealed class RegisterTriageHandler : MediatR.IRequestHandler<RegisterTriageCommand, RegisterTriageResult>
{
    private readonly IEmergencyRepository _er;
    private readonly IPatientRepository _patients;
    private readonly IBranchContext _ctx;
    private readonly IConfiguration _config;

    public RegisterTriageHandler(IEmergencyRepository er, IPatientRepository patients, IBranchContext ctx, IConfiguration config)
    { _er = er; _patients = patients; _ctx = ctx; _config = config; }

    public async Task<RegisterTriageResult> Handle(RegisterTriageCommand c, CancellationToken ct)
    {
        var branchId = _ctx.BranchId ?? throw new InvalidOperationException("Branch context required.");

        var categories = EmergencyConfig.TriageCategories(_config);
        if (!categories.Any(cat => string.Equals(cat, c.Category, StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException($"Invalid triage category '{c.Category}'. Allowed: {string.Join(", ", categories)}.");

        long? patientId = null;
        if (!string.IsNullOrWhiteSpace(c.PatientUhid))
        {
            var p = await _patients.GetByUhidAsync(LookupCode.Parse(c.PatientUhid!), ct)
                ?? throw new InvalidOperationException($"Unknown patient '{c.PatientUhid}'.");
            patientId = p.PatientId;
        }

        int? attendingId = string.IsNullOrWhiteSpace(c.AttendingDoctorCode)
            ? null : await _er.GetDoctorIdByCodeAsync(LookupCode.Parse(c.AttendingDoctorCode!), ct);

        var level = c.TriageLevel ?? TriageAcuity.LevelForColour(c.Category);   // derive level from colour if not given

        var id = await _er.InsertTriageAsync(new EmergencyTriage
        {
            BranchId = branchId,
            PatientId = patientId,
            ArrivedUtc = DateTime.UtcNow,
            Category = c.Category,
            IsMlc = c.IsMlc,
            Notes = c.Notes,
            Status = "Waiting",
            ChiefComplaint = c.ChiefComplaint,
            ArrivalMode = c.ArrivalMode,
            TriageLevel = level,
            PainScore = c.PainScore,
            GcsTotal = c.GcsTotal,
            TempF = c.TempF, Pulse = c.Pulse, BpSystolic = c.BpSystolic, BpDiastolic = c.BpDiastolic,
            Spo2 = c.Spo2, RespRate = c.RespRate, Grbs = c.Grbs,
            AttendingDoctorId = attendingId
        }, ct);

        return new RegisterTriageResult(id, c.Category, level, "Waiting");
    }
}

// ============================ Triage disposition — simple status (§3.5) ============================
public sealed record SetTriageDispositionCommand(long TriageId, string Status)
    : ICommand<bool>, IAuditable
{
    public string AuditEntity => "EmergencyTriage";
    public string? AuditEntityId => TriageId.ToString();
}

public sealed class SetTriageDispositionHandler : MediatR.IRequestHandler<SetTriageDispositionCommand, bool>
{
    private readonly IEmergencyRepository _er;
    private readonly IConfiguration _config;
    public SetTriageDispositionHandler(IEmergencyRepository er, IConfiguration config) { _er = er; _config = config; }

    public async Task<bool> Handle(SetTriageDispositionCommand c, CancellationToken ct)
    {
        var allowed = EmergencyConfig.TriageStatuses(_config);
        if (!allowed.Any(s => string.Equals(s, c.Status, StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException($"Invalid disposition '{c.Status}'. Allowed: {string.Join(", ", allowed)}.");

        _ = await _er.GetTriageAsync(c.TriageId, ct)
            ?? throw new InvalidOperationException("Triage record not found.");

        await _er.SetTriageStatusAsync(c.TriageId, c.Status, ct);
        return true;
    }
}

// ============================ Emergency disposition — admit / discharge (§3.5→§3.4) ============================
/// <summary>Final ED disposition. For AdmitICU/AdmitWard, creates a clinical.Admission
/// (emergency) to the chosen bed and links it back to the triage record.</summary>
public sealed record DisposeEmergencyVisitCommand(
    long TriageId, string Disposition, string? PatientUhid = null, string? BedLabel = null, string? ConsultantCode = null)
    : ICommand<DisposeEmergencyVisitResult>, IAuditable
{
    public string AuditEntity => "EmergencyTriage";
    public string? AuditEntityId => TriageId.ToString();
}
public sealed record DisposeEmergencyVisitResult(bool Ok, long? AdmissionId, string? AdmissionNo, string? BedNo);

public sealed class DisposeEmergencyVisitHandler : MediatR.IRequestHandler<DisposeEmergencyVisitCommand, DisposeEmergencyVisitResult>
{
    private readonly IEmergencyRepository _er;
    private readonly MediatR.ISender _sender;
    public DisposeEmergencyVisitHandler(IEmergencyRepository er, MediatR.ISender sender) { _er = er; _sender = sender; }

    public async Task<DisposeEmergencyVisitResult> Handle(DisposeEmergencyVisitCommand c, CancellationToken ct)
    {
        _ = await _er.GetTriageAsync(c.TriageId, ct)
            ?? throw new InvalidOperationException("Triage record not found.");

        long? admissionId = null; string? admissionNo = null, bedNo = null;
        var admits = c.Disposition is "AdmitICU" or "AdmitWard";
        if (admits)
        {
            if (string.IsNullOrWhiteSpace(c.PatientUhid) || string.IsNullOrWhiteSpace(c.BedLabel))
                throw new InvalidOperationException("Admission needs a patient and a bed.");
            var adm = await _sender.Send(new AdmitPatientCommand(
                c.PatientUhid!, c.BedLabel!, c.ConsultantCode, null, "Emergency", null, null), ct);
            admissionId = adm.AdmissionId; admissionNo = adm.AdmissionNo; bedNo = adm.BedNo;
        }

        await _er.DisposeAsync(c.TriageId, c.Disposition, admissionId, ct);
        return new DisposeEmergencyVisitResult(true, admissionId, admissionNo, bedNo);
    }
}

// ============================ ED board (§3.5) ============================
public sealed record TriageBoardRow(
    long TriageId, string? Patient, string? Uhid, string Category, byte? TriageLevel,
    string? ChiefComplaint, string? ArrivalMode, bool IsMlc, string Status, DateTime ArrivedUtc);
public sealed record GetTriageBoardQuery : IQuery<IReadOnlyList<TriageBoardRow>>;

public sealed class GetTriageBoardHandler : MediatR.IRequestHandler<GetTriageBoardQuery, IReadOnlyList<TriageBoardRow>>
{
    private readonly IEmergencyRepository _er;
    private readonly IBranchContext _ctx;
    private readonly IConfiguration _config;
    public GetTriageBoardHandler(IEmergencyRepository er, IBranchContext ctx, IConfiguration config)
    { _er = er; _ctx = ctx; _config = config; }

    public async Task<IReadOnlyList<TriageBoardRow>> Handle(GetTriageBoardQuery q, CancellationToken ct)
    {
        var rows = await _er.GetBoardAsync(_ctx.BranchId ?? 0, EmergencyConfig.TriageCategories(_config), ct);
        return rows.Select(r => new TriageBoardRow(r.Item1, r.Item2, r.Item3, r.Item4, r.Item5, r.Item6, r.Item7, r.Item8, r.Item9, r.Item10)).ToList();
    }
}

internal static class EmergencyConfig
{
    public static IReadOnlyList<string> TriageCategories(IConfiguration config) =>
        Split(config["Emergency:TriageCategories"], "Red,Orange,Yellow,Green,Blue");

    public static IReadOnlyList<string> TriageStatuses(IConfiguration config) =>
        Split(config["Emergency:TriageStatuses"], "Waiting,InTreatment,Admitted,Discharged");

    private static IReadOnlyList<string> Split(string? csv, string fallback) =>
        (string.IsNullOrWhiteSpace(csv) ? fallback : csv)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}
