using FluentValidation;
using HIS.Application.Abstractions;
using HIS.Application.Features.Appointments;   // LookupCode
using HIS.Domain.Entities;
using HIS.Shared.Context;
using Microsoft.Extensions.Configuration;

namespace HIS.Application.Features.Support;

// ============================ Consent (SRS §3.29) ============================
public sealed record ConsentTemplateDto(int TemplateId, string Code, string Title, string Lang);
public sealed record GetConsentTemplatesQuery : IQuery<IReadOnlyList<ConsentTemplateDto>>;
public sealed class GetConsentTemplatesHandler : MediatR.IRequestHandler<GetConsentTemplatesQuery, IReadOnlyList<ConsentTemplateDto>>
{
    private readonly IExperienceRepository _r;
    public GetConsentTemplatesHandler(IExperienceRepository r) { _r = r; }
    public async Task<IReadOnlyList<ConsentTemplateDto>> Handle(GetConsentTemplatesQuery q, CancellationToken ct)
        => (await _r.GetConsentTemplatesAsync(ct)).Select(t => new ConsentTemplateDto(t.TemplateId, t.Code, t.Title, t.Lang)).ToList();
}
public sealed record CaptureConsentCommand(int TemplateId, string PatientUhid, string? SignatureType) : ICommand<long>, IAuditable
{
    public string AuditEntity => "ConsentCapture";
    public string? AuditEntityId => PatientUhid;
}
public sealed class CaptureConsentValidator : AbstractValidator<CaptureConsentCommand>
{
    public CaptureConsentValidator() { RuleFor(x => x.TemplateId).GreaterThan(0); RuleFor(x => x.PatientUhid).NotEmpty(); }
}
public sealed class CaptureConsentHandler : MediatR.IRequestHandler<CaptureConsentCommand, long>
{
    private readonly IExperienceRepository _r; private readonly IPatientRepository _patients;
    public CaptureConsentHandler(IExperienceRepository r, IPatientRepository patients) { _r = r; _patients = patients; }
    public async Task<long> Handle(CaptureConsentCommand c, CancellationToken ct)
    {
        var patient = await _patients.GetByUhidAsync(LookupCode.Parse(c.PatientUhid), ct)
            ?? throw new InvalidOperationException($"Unknown patient '{c.PatientUhid}'.");
        return await _r.InsertConsentCaptureAsync(new ConsentCapture
        { TemplateId = c.TemplateId, PatientId = patient.PatientId, SignatureType = c.SignatureType ?? "e-Signature", CapturedUtc = DateTime.UtcNow }, ct);
    }
}

public sealed record ConsentCaptureRowDto(long ConsentId, string Patient, string Template, string? SignatureType, string CapturedUtc);
public sealed record GetConsentCapturesQuery : IQuery<IReadOnlyList<ConsentCaptureRowDto>>;
public sealed class GetConsentCapturesHandler : MediatR.IRequestHandler<GetConsentCapturesQuery, IReadOnlyList<ConsentCaptureRowDto>>
{
    private readonly IExperienceRepository _r;
    public GetConsentCapturesHandler(IExperienceRepository r) { _r = r; }
    public async Task<IReadOnlyList<ConsentCaptureRowDto>> Handle(GetConsentCapturesQuery q, CancellationToken ct)
        => (await _r.GetConsentCapturesAsync(ct)).Select(r => new ConsentCaptureRowDto(
            r.ConsentId, r.Patient, r.Template, r.SignatureType, r.CapturedUtc.ToString("yyyy-MM-dd HH:mm"))).ToList();
}

// ============================ Certificates (SRS §3.16) ============================
public sealed record CertTemplateDto(int TemplateId, string CertType, string Title);
public sealed record GetCertTemplatesQuery : IQuery<IReadOnlyList<CertTemplateDto>>;
public sealed class GetCertTemplatesHandler : MediatR.IRequestHandler<GetCertTemplatesQuery, IReadOnlyList<CertTemplateDto>>
{
    private readonly IExperienceRepository _r;
    public GetCertTemplatesHandler(IExperienceRepository r) { _r = r; }
    public async Task<IReadOnlyList<CertTemplateDto>> Handle(GetCertTemplatesQuery q, CancellationToken ct)
        => (await _r.GetCertTemplatesAsync(ct)).Select(t => new CertTemplateDto(t.TemplateId, t.CertType, t.Title)).ToList();
}
public sealed record IssueCertificateCommand(int TemplateId, string PatientUhid) : ICommand<long>, IAuditable
{
    public string AuditEntity => "IssuedCertificate";
    public string? AuditEntityId => PatientUhid;
}
public sealed class IssueCertificateValidator : AbstractValidator<IssueCertificateCommand>
{
    public IssueCertificateValidator() { RuleFor(x => x.TemplateId).GreaterThan(0); RuleFor(x => x.PatientUhid).NotEmpty(); }
}
public sealed class IssueCertificateHandler : MediatR.IRequestHandler<IssueCertificateCommand, long>
{
    private readonly IExperienceRepository _r; private readonly IPatientRepository _patients;
    public IssueCertificateHandler(IExperienceRepository r, IPatientRepository patients) { _r = r; _patients = patients; }
    public async Task<long> Handle(IssueCertificateCommand c, CancellationToken ct)
    {
        var patient = await _patients.GetByUhidAsync(LookupCode.Parse(c.PatientUhid), ct)
            ?? throw new InvalidOperationException($"Unknown patient '{c.PatientUhid}'.");
        return await _r.InsertCertificateAsync(new IssuedCertificate
        { TemplateId = c.TemplateId, PatientId = patient.PatientId, IssuedUtc = DateTime.UtcNow, Status = "Draft" }, ct);
    }
}
public sealed record ApproveCertificateCommand(long CertId, string DoctorCode) : ICommand<bool>, IAuditable
{
    public string AuditEntity => "IssuedCertificate";
    public string? AuditEntityId => CertId.ToString();
}
public sealed class ApproveCertificateHandler : MediatR.IRequestHandler<ApproveCertificateCommand, bool>
{
    private readonly IExperienceRepository _r; private readonly IAppointmentRepository _doctors;
    public ApproveCertificateHandler(IExperienceRepository r, IAppointmentRepository doctors) { _r = r; _doctors = doctors; }
    public async Task<bool> Handle(ApproveCertificateCommand c, CancellationToken ct)
    {
        var doctorId = await _doctors.GetDoctorIdByCodeAsync(LookupCode.Parse(c.DoctorCode), ct)
            ?? throw new InvalidOperationException($"Unknown doctor '{c.DoctorCode}'.");
        await _r.ApproveCertificateAsync(c.CertId, doctorId, ct);
        return true;
    }
}
public sealed record CertRowDto(long CertId, string CertType, string Patient, string Status);
public sealed record GetCertificatesQuery : IQuery<IReadOnlyList<CertRowDto>>;
public sealed class GetCertificatesHandler : MediatR.IRequestHandler<GetCertificatesQuery, IReadOnlyList<CertRowDto>>
{
    private readonly IExperienceRepository _r; private readonly IBranchContext _ctx;
    public GetCertificatesHandler(IExperienceRepository r, IBranchContext ctx) { _r = r; _ctx = ctx; }
    public async Task<IReadOnlyList<CertRowDto>> Handle(GetCertificatesQuery q, CancellationToken ct)
        => (await _r.GetCertificatesAsync(_ctx.BranchId ?? 0, ct)).Select(c => new CertRowDto(c.CertId, c.CertType, c.Patient, c.Status)).ToList();
}

// ============================ Feedback & Grievance (SRS §3.30) ============================
public sealed record SubmitFeedbackCommand(string? PatientUhid, int Score, string? Comments) : ICommand<long>, IAuditable
{
    public string AuditEntity => "FeedbackSurvey";
    public string? AuditEntityId => PatientUhid;
}
public sealed class SubmitFeedbackValidator : AbstractValidator<SubmitFeedbackCommand>
{
    public SubmitFeedbackValidator() => RuleFor(x => x.Score).InclusiveBetween(1, 5);
}
public sealed class SubmitFeedbackHandler : MediatR.IRequestHandler<SubmitFeedbackCommand, long>
{
    private readonly IExperienceRepository _r; private readonly IPatientRepository _patients;
    public SubmitFeedbackHandler(IExperienceRepository r, IPatientRepository patients) { _r = r; _patients = patients; }
    public async Task<long> Handle(SubmitFeedbackCommand c, CancellationToken ct)
    {
        long? patientId = string.IsNullOrWhiteSpace(c.PatientUhid) ? null
            : (await _patients.GetByUhidAsync(LookupCode.Parse(c.PatientUhid!), ct))?.PatientId;
        return await _r.InsertSurveyAsync(new FeedbackSurvey { PatientId = patientId, Score = c.Score, Comments = c.Comments, CreatedUtc = DateTime.UtcNow }, ct);
    }
}
public sealed record LogGrievanceCommand(string? PatientUhid, string Category) : ICommand<long>, IAuditable
{
    public string AuditEntity => "Grievance";
    public string? AuditEntityId => Category;
}
public sealed class LogGrievanceValidator : AbstractValidator<LogGrievanceCommand>
{
    public LogGrievanceValidator() => RuleFor(x => x.Category).NotEmpty();
}
public sealed class LogGrievanceHandler : MediatR.IRequestHandler<LogGrievanceCommand, long>
{
    private readonly IExperienceRepository _r; private readonly IPatientRepository _patients; private readonly IBranchContext _ctx; private readonly IConfiguration _config;
    public LogGrievanceHandler(IExperienceRepository r, IPatientRepository patients, IBranchContext ctx, IConfiguration config)
    { _r = r; _patients = patients; _ctx = ctx; _config = config; }
    public async Task<long> Handle(LogGrievanceCommand c, CancellationToken ct)
    {
        long? patientId = string.IsNullOrWhiteSpace(c.PatientUhid) ? null
            : (await _patients.GetByUhidAsync(LookupCode.Parse(c.PatientUhid!), ct))?.PatientId;
        var slaHours = _config.GetValue("Grievance:SlaHours", 24);   // SLA window from config, not hardcoded
        return await _r.InsertGrievanceAsync(new Grievance
        { BranchId = _ctx.BranchId ?? 0, PatientId = patientId, Category = c.Category, SlaDueUtc = DateTime.UtcNow.AddHours(slaHours), Status = "Open", CreatedUtc = DateTime.UtcNow }, ct);
    }
}
public sealed record ResolveGrievanceCommand(long GrievanceId, int TatMinutes) : ICommand<bool>, IAuditable
{
    public string AuditEntity => "Grievance";
    public string? AuditEntityId => GrievanceId.ToString();
}
public sealed class ResolveGrievanceHandler : MediatR.IRequestHandler<ResolveGrievanceCommand, bool>
{
    private readonly IExperienceRepository _r;
    public ResolveGrievanceHandler(IExperienceRepository r) { _r = r; }
    public async Task<bool> Handle(ResolveGrievanceCommand c, CancellationToken ct) { await _r.ResolveGrievanceAsync(c.GrievanceId, c.TatMinutes, ct); return true; }
}
public sealed record GrievanceRowDto(long GrievanceId, string? Category, string Status, string Created);
public sealed record GetGrievancesQuery : IQuery<IReadOnlyList<GrievanceRowDto>>;
public sealed class GetGrievancesHandler : MediatR.IRequestHandler<GetGrievancesQuery, IReadOnlyList<GrievanceRowDto>>
{
    private readonly IExperienceRepository _r; private readonly IBranchContext _ctx;
    public GetGrievancesHandler(IExperienceRepository r, IBranchContext ctx) { _r = r; _ctx = ctx; }
    public async Task<IReadOnlyList<GrievanceRowDto>> Handle(GetGrievancesQuery q, CancellationToken ct)
        => (await _r.GetGrievancesAsync(_ctx.BranchId ?? 0, ct)).Select(g => new GrievanceRowDto(g.GrievanceId, g.Category, g.Status, g.Created)).ToList();
}

// ============================ Queue (SRS §3.31) ============================
public sealed record CounterDto(int CounterId, string Area, string CounterName);
public sealed record GetCountersQuery : IQuery<IReadOnlyList<CounterDto>>;
public sealed class GetCountersHandler : MediatR.IRequestHandler<GetCountersQuery, IReadOnlyList<CounterDto>>
{
    private readonly IExperienceRepository _r; private readonly IBranchContext _ctx;
    public GetCountersHandler(IExperienceRepository r, IBranchContext ctx) { _r = r; _ctx = ctx; }
    public async Task<IReadOnlyList<CounterDto>> Handle(GetCountersQuery q, CancellationToken ct)
        => (await _r.GetCountersAsync(_ctx.BranchId ?? 0, ct)).Select(c => new CounterDto(c.CounterId, c.Area, c.CounterName)).ToList();
}
public sealed record IssueTokenCommand(int CounterId, string? PatientUhid) : ICommand<string>, IAuditable
{
    public string AuditEntity => "QueueToken";
    public string? AuditEntityId => CounterId.ToString();
}
public sealed class IssueTokenHandler : MediatR.IRequestHandler<IssueTokenCommand, string>
{
    private readonly IExperienceRepository _r; private readonly IPatientRepository _patients;
    public IssueTokenHandler(IExperienceRepository r, IPatientRepository patients) { _r = r; _patients = patients; }
    public async Task<string> Handle(IssueTokenCommand c, CancellationToken ct)
    {
        long? patientId = string.IsNullOrWhiteSpace(c.PatientUhid) ? null
            : (await _patients.GetByUhidAsync(LookupCode.Parse(c.PatientUhid!), ct))?.PatientId;
        return await _r.IssueTokenAsync(c.CounterId, patientId, ct);
    }
}
public sealed record CallNextCommand(int CounterId) : ICommand<string?>, IAuditable
{
    public string AuditEntity => "QueueToken";
    public string? AuditEntityId => CounterId.ToString();
}
public sealed class CallNextHandler : MediatR.IRequestHandler<CallNextCommand, string?>
{
    private readonly IExperienceRepository _r;
    public CallNextHandler(IExperienceRepository r) { _r = r; }
    public Task<string?> Handle(CallNextCommand c, CancellationToken ct) => _r.CallNextAsync(c.CounterId, ct);
}
public sealed record QueueRowDto(string Area, string Counter, string TokenNo, string Status);
public sealed record GetQueueQuery : IQuery<IReadOnlyList<QueueRowDto>>;
public sealed class GetQueueHandler : MediatR.IRequestHandler<GetQueueQuery, IReadOnlyList<QueueRowDto>>
{
    private readonly IExperienceRepository _r; private readonly IBranchContext _ctx;
    public GetQueueHandler(IExperienceRepository r, IBranchContext ctx) { _r = r; _ctx = ctx; }
    public async Task<IReadOnlyList<QueueRowDto>> Handle(GetQueueQuery q, CancellationToken ct)
        => (await _r.GetQueueAsync(_ctx.BranchId ?? 0, ct)).Select(x => new QueueRowDto(x.Area, x.Counter, x.TokenNo, x.Status)).ToList();
}
