using FluentValidation;
using HIS.Application.Abstractions;
using HIS.Application.Features.Appointments;   // LookupCode
using HIS.Domain.Entities;
using HIS.Shared.Context;

namespace HIS.Application.Features.Telemedicine;

// ============================ Schedule teleconsult (SRS §3.24) ============================
public sealed record ScheduleTeleConsultCommand(string PatientUhid, string DoctorCode, string ConsultType, DateTime ScheduledUtc, int? ToBranchId)
    : ICommand<long>, IAuditable
{
    public string AuditEntity => "TeleConsult";
    public string? AuditEntityId => PatientUhid;
}

public sealed class ScheduleTeleConsultValidator : AbstractValidator<ScheduleTeleConsultCommand>
{
    public ScheduleTeleConsultValidator()
    {
        RuleFor(x => x.PatientUhid).NotEmpty();
        RuleFor(x => x.DoctorCode).NotEmpty();
        RuleFor(x => x.ConsultType).NotEmpty();
    }
}

public sealed class ScheduleTeleConsultHandler : MediatR.IRequestHandler<ScheduleTeleConsultCommand, long>
{
    private readonly ITelemedicineRepository _tele;
    private readonly IPatientRepository _patients;
    private readonly IBranchContext _ctx;
    public ScheduleTeleConsultHandler(ITelemedicineRepository tele, IPatientRepository patients, IBranchContext ctx)
    { _tele = tele; _patients = patients; _ctx = ctx; }

    public async Task<long> Handle(ScheduleTeleConsultCommand c, CancellationToken ct)
    {
        var patient = await _patients.GetByUhidAsync(LookupCode.Parse(c.PatientUhid), ct)
            ?? throw new InvalidOperationException($"Unknown patient '{c.PatientUhid}'.");
        var doctorId = await _tele.GetDoctorIdByCodeAsync(LookupCode.Parse(c.DoctorCode), ct);
        return await _tele.InsertTeleAsync(new TeleConsult
        {
            PatientId = patient.PatientId, DoctorId = doctorId, FromBranchId = _ctx.BranchId, ToBranchId = c.ToBranchId,
            ConsultType = c.ConsultType, ScheduledUtc = c.ScheduledUtc, Status = "Scheduled"
        }, ct);
    }
}

// ============================ Lifecycle actions (SRS §3.24) ============================
public sealed record CaptureTeleConsentCommand(long TeleId) : ICommand<bool>, IAuditable
{
    public string AuditEntity => "TeleConsult";
    public string? AuditEntityId => TeleId.ToString();
}

public sealed class CaptureTeleConsentHandler : MediatR.IRequestHandler<CaptureTeleConsentCommand, bool>
{
    private readonly ITelemedicineRepository _tele;
    public CaptureTeleConsentHandler(ITelemedicineRepository tele) { _tele = tele; }

    public async Task<bool> Handle(CaptureTeleConsentCommand c, CancellationToken ct)
    {
        var t = await _tele.GetTeleAsync(c.TeleId, ct) ?? throw new InvalidOperationException("Teleconsult not found.");
        t.ConsentCaptured = true;
        await _tele.UpdateTeleAsync(t, ct);
        return true;
    }
}

public sealed record SignEPrescriptionCommand(long TeleId) : ICommand<bool>, IAuditable
{
    public string AuditEntity => "TeleConsult";
    public string? AuditEntityId => TeleId.ToString();
}

public sealed class SignEPrescriptionHandler : MediatR.IRequestHandler<SignEPrescriptionCommand, bool>
{
    private readonly ITelemedicineRepository _tele;
    public SignEPrescriptionHandler(ITelemedicineRepository tele) { _tele = tele; }

    public async Task<bool> Handle(SignEPrescriptionCommand c, CancellationToken ct)
    {
        var t = await _tele.GetTeleAsync(c.TeleId, ct) ?? throw new InvalidOperationException("Teleconsult not found.");
        // TPG 2020: patient consent must be captured before an e-prescription is issued.
        if (!t.ConsentCaptured) throw new InvalidOperationException("Capture patient consent before signing the e-prescription.");
        t.EPrescriptionSigned = true;
        await _tele.UpdateTeleAsync(t, ct);
        return true;
    }
}

public sealed record CompleteTeleConsultCommand(long TeleId, string? SessionAuditUrl) : ICommand<bool>, IAuditable
{
    public string AuditEntity => "TeleConsult";
    public string? AuditEntityId => TeleId.ToString();
}

public sealed class CompleteTeleConsultHandler : MediatR.IRequestHandler<CompleteTeleConsultCommand, bool>
{
    private readonly ITelemedicineRepository _tele;
    public CompleteTeleConsultHandler(ITelemedicineRepository tele) { _tele = tele; }

    public async Task<bool> Handle(CompleteTeleConsultCommand c, CancellationToken ct)
    {
        var t = await _tele.GetTeleAsync(c.TeleId, ct) ?? throw new InvalidOperationException("Teleconsult not found.");
        if (!t.ConsentCaptured) throw new InvalidOperationException("Consent required before completing the session.");
        t.Status = "Completed";
        t.SessionAuditUrl = c.SessionAuditUrl ?? $"audit/tele/{c.TeleId}";
        await _tele.UpdateTeleAsync(t, ct);
        return true;
    }
}

// ============================ List (SRS §3.24) ============================
public sealed record TeleRowDto(long TeleId, string Patient, string? Doctor, string? ConsultType, string? Scheduled, bool Consent, bool Signed, string Status);
public sealed record GetTeleConsultsQuery : IQuery<IReadOnlyList<TeleRowDto>>;

public sealed class GetTeleConsultsHandler : MediatR.IRequestHandler<GetTeleConsultsQuery, IReadOnlyList<TeleRowDto>>
{
    private readonly ITelemedicineRepository _tele;
    private readonly IBranchContext _ctx;
    public GetTeleConsultsHandler(ITelemedicineRepository tele, IBranchContext ctx) { _tele = tele; _ctx = ctx; }

    public async Task<IReadOnlyList<TeleRowDto>> Handle(GetTeleConsultsQuery q, CancellationToken ct)
        => (await _tele.GetTeleListAsync(_ctx.BranchId ?? 0, ct))
            .Select(t => new TeleRowDto(t.TeleId, t.Patient, t.Doctor, t.ConsultType, t.Scheduled, t.Consent, t.Signed, t.Status)).ToList();
}
