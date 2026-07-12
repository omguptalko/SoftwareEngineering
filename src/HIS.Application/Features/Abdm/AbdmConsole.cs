using FluentValidation;
using HIS.Application.Abstractions;
using HIS.Application.Features.Appointments;   // LookupCode
using HIS.Domain.Entities;

namespace HIS.Application.Features.Abdm;

// =====================================================================
// ABDM / ABHA Console (SRS §6.2) — consent artifacts (HIP/HIU) + HFR/HPR
// registry onboarding. All data is longitudinal (tenant master DB). Every
// write is audited via the IAuditable pipeline. Nothing hardcoded.
// =====================================================================

// ---------------------- Consent artifacts ----------------------------
public sealed record ConsentRowDto(long ConsentArtifactId, string Patient, string? AbhaNumber, string? Purpose,
    string? HiTypes, string Status, string? GrantedUtc, string? ExpiryUtc);
public sealed record GetAbdmConsentsQuery : IQuery<IReadOnlyList<ConsentRowDto>>, IRequireAuthentication;

public sealed class GetAbdmConsentsHandler : MediatR.IRequestHandler<GetAbdmConsentsQuery, IReadOnlyList<ConsentRowDto>>
{
    private readonly IAbdmRepository _r;
    public GetAbdmConsentsHandler(IAbdmRepository r) => _r = r;
    public async Task<IReadOnlyList<ConsentRowDto>> Handle(GetAbdmConsentsQuery q, CancellationToken ct)
        => (await _r.GetConsentsAsync(ct)).Select(x => new ConsentRowDto(
            x.ConsentArtifactId, x.Patient, x.AbhaNumber, x.Purpose, x.HiTypes, x.Status,
            x.GrantedUtc?.ToString("yyyy-MM-dd HH:mm"), x.ExpiryUtc?.ToString("yyyy-MM-dd"))).ToList();
}

public sealed record RequestConsentCommand(string PatientUhid, string Purpose, IReadOnlyList<string>? HiTypes, DateTime? Expiry)
    : ICommand<long>, IAuditable
{
    public string AuditEntity => "AbdmConsent";
    public string? AuditEntityId => PatientUhid;
}

public sealed class RequestConsentValidator : AbstractValidator<RequestConsentCommand>
{
    public RequestConsentValidator()
    {
        RuleFor(x => x.PatientUhid).NotEmpty();
        RuleFor(x => x.Purpose).NotEmpty();
    }
}

public sealed class RequestConsentHandler : MediatR.IRequestHandler<RequestConsentCommand, long>
{
    private readonly IAbdmRepository _r;
    private readonly IPatientRepository _patients;
    public RequestConsentHandler(IAbdmRepository r, IPatientRepository patients) { _r = r; _patients = patients; }

    public async Task<long> Handle(RequestConsentCommand c, CancellationToken ct)
    {
        var patient = await _patients.GetByUhidAsync(LookupCode.Parse(c.PatientUhid), ct)
            ?? throw new InvalidOperationException($"Unknown patient '{c.PatientUhid}'.");
        var hiTypes = c.HiTypes is { Count: > 0 } ? string.Join(",", c.HiTypes) : null;
        return await _r.InsertConsentAsync(new AbdmConsent
        {
            PatientId = patient.PatientId,
            AbhaNumber = patient.AbhaNumber,
            Purpose = c.Purpose,
            HiTypes = hiTypes,
            ExpiryUtc = c.Expiry,
            Status = "Requested",
            // The care-context document that would be shared over ABDM is this patient's FHIR R4 export.
            FhirBundleUrl = $"/api/fhir/Patient/{patient.Uhid}"
        }, ct);
    }
}

public sealed record SetConsentStatusCommand(long ConsentArtifactId, string Action, int? ValidityMonths)
    : ICommand<bool>, IAuditable
{
    public string AuditEntity => "AbdmConsent";
    public string? AuditEntityId => ConsentArtifactId.ToString();
}

public sealed class SetConsentStatusValidator : AbstractValidator<SetConsentStatusCommand>
{
    public SetConsentStatusValidator()
    {
        RuleFor(x => x.ConsentArtifactId).GreaterThan(0);
        RuleFor(x => x.Action).Must(a => a is "grant" or "revoke")
            .WithMessage("Action must be 'grant' or 'revoke'.");
    }
}

public sealed class SetConsentStatusHandler : MediatR.IRequestHandler<SetConsentStatusCommand, bool>
{
    private readonly IAbdmRepository _r;
    public SetConsentStatusHandler(IAbdmRepository r) => _r = r;

    public async Task<bool> Handle(SetConsentStatusCommand c, CancellationToken ct)
    {
        var existing = await _r.GetConsentAsync(c.ConsentArtifactId, ct)
            ?? throw new InvalidOperationException($"Consent artifact {c.ConsentArtifactId} not found.");

        if (c.Action == "revoke")
        {
            await _r.UpdateConsentStatusAsync(c.ConsentArtifactId, "Revoked", null, null, ct);
            return true;
        }

        // grant
        var now = DateTime.UtcNow;
        // Keep an explicit expiry if the request already set one; else derive from the granted validity window.
        var expiry = existing.ExpiryUtc ?? (c.ValidityMonths is int m && m > 0 ? now.AddMonths(m) : (DateTime?)null);
        await _r.UpdateConsentStatusAsync(c.ConsentArtifactId, "Granted", now, expiry, ct);
        return true;
    }
}

// ---------------------- HFR facilities --------------------------------
public sealed record FacilityRowDto(int HfrId, string Branch, string? HfrCode, string? OnboardedUtc);
public sealed record GetHfrFacilitiesQuery : IQuery<IReadOnlyList<FacilityRowDto>>, IRequireAuthentication;

public sealed class GetHfrFacilitiesHandler : MediatR.IRequestHandler<GetHfrFacilitiesQuery, IReadOnlyList<FacilityRowDto>>
{
    private readonly IAbdmRepository _r;
    public GetHfrFacilitiesHandler(IAbdmRepository r) => _r = r;
    public async Task<IReadOnlyList<FacilityRowDto>> Handle(GetHfrFacilitiesQuery q, CancellationToken ct)
        => (await _r.GetFacilitiesAsync(ct)).Select(x => new FacilityRowDto(
            x.HfrId, x.Branch, x.HfrCode, x.OnboardedUtc?.ToString("yyyy-MM-dd HH:mm"))).ToList();
}

public sealed record OnboardFacilityCommand(int BranchId, string HfrCode) : ICommand<int>, IAuditable
{
    public string AuditEntity => "HfrFacility";
    public string? AuditEntityId => HfrCode;
}

public sealed class OnboardFacilityValidator : AbstractValidator<OnboardFacilityCommand>
{
    public OnboardFacilityValidator()
    {
        RuleFor(x => x.BranchId).GreaterThan(0);
        RuleFor(x => x.HfrCode).NotEmpty();
    }
}

public sealed class OnboardFacilityHandler : MediatR.IRequestHandler<OnboardFacilityCommand, int>
{
    private readonly IAbdmRepository _r;
    public OnboardFacilityHandler(IAbdmRepository r) => _r = r;
    public Task<int> Handle(OnboardFacilityCommand c, CancellationToken ct)
        => _r.UpsertFacilityAsync(c.BranchId, c.HfrCode.Trim(), ct);
}

// ---------------------- HPR professionals -----------------------------
public sealed record ProfessionalRowDto(int HprId, string Doctor, string? Department, string? HprCode, string? OnboardedUtc);
public sealed record GetHprProfessionalsQuery : IQuery<IReadOnlyList<ProfessionalRowDto>>, IRequireAuthentication;

public sealed class GetHprProfessionalsHandler : MediatR.IRequestHandler<GetHprProfessionalsQuery, IReadOnlyList<ProfessionalRowDto>>
{
    private readonly IAbdmRepository _r;
    public GetHprProfessionalsHandler(IAbdmRepository r) => _r = r;
    public async Task<IReadOnlyList<ProfessionalRowDto>> Handle(GetHprProfessionalsQuery q, CancellationToken ct)
        => (await _r.GetProfessionalsAsync(ct)).Select(x => new ProfessionalRowDto(
            x.HprId, x.Doctor, x.Department, x.HprCode, x.OnboardedUtc?.ToString("yyyy-MM-dd HH:mm"))).ToList();
}

public sealed record OnboardProfessionalCommand(string DoctorCode, string HprCode) : ICommand<int>, IAuditable
{
    public string AuditEntity => "HprProfessional";
    public string? AuditEntityId => HprCode;
}

public sealed class OnboardProfessionalValidator : AbstractValidator<OnboardProfessionalCommand>
{
    public OnboardProfessionalValidator()
    {
        RuleFor(x => x.DoctorCode).NotEmpty();
        RuleFor(x => x.HprCode).NotEmpty();
    }
}

public sealed class OnboardProfessionalHandler : MediatR.IRequestHandler<OnboardProfessionalCommand, int>
{
    private readonly IAbdmRepository _r;
    public OnboardProfessionalHandler(IAbdmRepository r) => _r = r;

    public async Task<int> Handle(OnboardProfessionalCommand c, CancellationToken ct)
    {
        var doctorId = await _r.GetDoctorIdByCodeAsync(LookupCode.Parse(c.DoctorCode), ct)
            ?? throw new InvalidOperationException($"Unknown doctor '{c.DoctorCode}'.");
        return await _r.UpsertProfessionalAsync(doctorId, c.HprCode.Trim(), ct);
    }
}
