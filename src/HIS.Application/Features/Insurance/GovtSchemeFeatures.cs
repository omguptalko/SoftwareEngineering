using FluentValidation;
using HIS.Application.Abstractions;
using HIS.Application.Features.Appointments;   // LookupCode
using HIS.Domain.Entities;
using HIS.Shared.Context;

namespace HIS.Application.Features.Insurance;

// ============================ PM-JAY (SRS §7.3) ============================
public sealed record VerifyPmjayBeneficiaryCommand(string PatientUhid, string PmjayId, decimal? FamilyFloater)
    : ICommand<VerifyPmjayResult>, IAuditable
{
    public string AuditEntity => "PmjayBeneficiary";
    public string? AuditEntityId => PmjayId;
}
public sealed record VerifyPmjayResult(long BeneficiaryId, bool BisVerified);

public sealed class VerifyPmjayValidator : AbstractValidator<VerifyPmjayBeneficiaryCommand>
{
    public VerifyPmjayValidator()
    {
        RuleFor(x => x.PatientUhid).NotEmpty();
        RuleFor(x => x.PmjayId).NotEmpty();
    }
}

public sealed class VerifyPmjayHandler : MediatR.IRequestHandler<VerifyPmjayBeneficiaryCommand, VerifyPmjayResult>
{
    private readonly IPmjayRepository _pmjay;
    private readonly IPatientRepository _patients;
    public VerifyPmjayHandler(IPmjayRepository pmjay, IPatientRepository patients) { _pmjay = pmjay; _patients = patients; }

    public async Task<VerifyPmjayResult> Handle(VerifyPmjayBeneficiaryCommand c, CancellationToken ct)
    {
        var patient = await _patients.GetByUhidAsync(LookupCode.Parse(c.PatientUhid), ct)
            ?? throw new InvalidOperationException($"Unknown patient '{c.PatientUhid}'.");
        // BIS verification (Aadhaar e-KYC / PM-JAY ID) — represented as a verified flag.
        var id = await _pmjay.UpsertBeneficiaryAsync(new PmjayBeneficiary
        {
            PatientId = patient.PatientId, PmjayId = c.PmjayId, BisVerified = true,
            FamilyFloater = c.FamilyFloater, UsedAmount = 0
        }, ct);
        return new VerifyPmjayResult(id, true);
    }
}

public sealed record CreatePmjayClaimCommand(string PatientUhid, string PackageCode, string? AyushmanMitra)
    : ICommand<CreatePmjayClaimResult>, IAuditable
{
    public string AuditEntity => "PmjayCase";
    public string? AuditEntityId => PatientUhid;
}
public sealed record CreatePmjayClaimResult(long ClaimId, string ClaimNo, string TmsCaseNo, decimal PackageRate);

public sealed class CreatePmjayClaimValidator : AbstractValidator<CreatePmjayClaimCommand>
{
    public CreatePmjayClaimValidator()
    {
        RuleFor(x => x.PatientUhid).NotEmpty();
        RuleFor(x => x.PackageCode).NotEmpty();
    }
}

public sealed class CreatePmjayClaimHandler : MediatR.IRequestHandler<CreatePmjayClaimCommand, CreatePmjayClaimResult>
{
    private readonly IClaimsRepository _claims;
    private readonly IPmjayRepository _pmjay;
    private readonly IPatientRepository _patients;
    private readonly IBranchContext _ctx;

    public CreatePmjayClaimHandler(IClaimsRepository claims, IPmjayRepository pmjay, IPatientRepository patients, IBranchContext ctx)
    { _claims = claims; _pmjay = pmjay; _patients = patients; _ctx = ctx; }

    public async Task<CreatePmjayClaimResult> Handle(CreatePmjayClaimCommand c, CancellationToken ct)
    {
        var branchId = _ctx.BranchId ?? throw new InvalidOperationException("Branch context required.");
        var patient = await _patients.GetByUhidAsync(LookupCode.Parse(c.PatientUhid), ct)
            ?? throw new InvalidOperationException($"Unknown patient '{c.PatientUhid}'.");
        var payerId = await _claims.GetPayerIdByCodeAsync("PMJAY", ct)
            ?? throw new InvalidOperationException("PM-JAY payer not configured.");
        var pkg = await _pmjay.GetPackageByCodeAsync(LookupCode.Parse(c.PackageCode), ct)
            ?? throw new InvalidOperationException($"Unknown HBP package '{c.PackageCode}'.");

        var now = DateTime.UtcNow;
        var claimNo = await _claims.NextClaimNoAsync(branchId, ct);
        var claimId = await _claims.InsertClaimAsync(new Claim
        {
            ClaimNo = claimNo, BranchId = branchId, PatientId = patient.PatientId, PayerId = payerId,
            Channel = "TMS", PreAuthAmount = pkg.Rate, Status = "PreAuth", SubmittedUtc = now
        }, ct);
        await _claims.AddEventAsync(new ClaimEvent { ClaimId = claimId, EventType = "PreAuth", Amount = pkg.Rate, OccurredUtc = now }, ct);

        var tmsNo = await _pmjay.NextTmsNoAsync(branchId, ct);
        await _pmjay.InsertCaseAsync(new PmjayCase
        {
            ClaimId = claimId, PackageId = pkg.PackageId, TmsCaseNo = tmsNo, AyushmanMitra = c.AyushmanMitra
        }, ct);

        return new CreatePmjayClaimResult(claimId, claimNo, tmsNo, pkg.Rate);
    }
}

// ---- Submitted TMS claims list (PM-JAY page) ----
public sealed record PmjayCaseRowDto(string TmsCaseNo, string ClaimNo, string Patient, string Package, decimal? Amount, string Status, string? SubmittedUtc);
public sealed record GetPmjayCasesQuery : IQuery<IReadOnlyList<PmjayCaseRowDto>>;

public sealed class GetPmjayCasesHandler : MediatR.IRequestHandler<GetPmjayCasesQuery, IReadOnlyList<PmjayCaseRowDto>>
{
    private readonly IPmjayRepository _pmjay;
    private readonly IBranchContext _ctx;
    public GetPmjayCasesHandler(IPmjayRepository pmjay, IBranchContext ctx) { _pmjay = pmjay; _ctx = ctx; }

    public async Task<IReadOnlyList<PmjayCaseRowDto>> Handle(GetPmjayCasesQuery q, CancellationToken ct)
    {
        var rows = await _pmjay.GetCasesAsync(_ctx.BranchId ?? 0, ct);
        return rows.Select(r => new PmjayCaseRowDto(
            r.TmsCaseNo, r.ClaimNo, r.Patient, r.Package, r.Amount, r.Status,
            r.SubmittedUtc?.ToString("yyyy-MM-dd"))).ToList();
    }
}

// ============================ ESIC/CGHS/ECHS/State (SRS §7.4–§7.7) ============================
public sealed record VerifySchemeMembershipCommand(string PatientUhid, string SchemeType, string MemberNo, string? SecondaryRef)
    : ICommand<long>, IAuditable
{
    public string AuditEntity => "SchemeMembership";
    public string? AuditEntityId => $"{SchemeType}:{MemberNo}";
}

public sealed class VerifySchemeValidator : AbstractValidator<VerifySchemeMembershipCommand>
{
    public VerifySchemeValidator()
    {
        RuleFor(x => x.PatientUhid).NotEmpty();
        RuleFor(x => x.SchemeType).NotEmpty().Must(s => _types.Contains(s)).WithMessage("Unknown scheme type.");
        RuleFor(x => x.MemberNo).NotEmpty();
    }
    private static readonly HashSet<string> _types = new(StringComparer.OrdinalIgnoreCase) { "ESIC", "CGHS", "ECHS", "State" };
}

public sealed class VerifySchemeHandler : MediatR.IRequestHandler<VerifySchemeMembershipCommand, long>
{
    private readonly ISchemeRepository _scheme;
    private readonly IPatientRepository _patients;
    public VerifySchemeHandler(ISchemeRepository scheme, IPatientRepository patients) { _scheme = scheme; _patients = patients; }

    public async Task<long> Handle(VerifySchemeMembershipCommand c, CancellationToken ct)
    {
        var patient = await _patients.GetByUhidAsync(LookupCode.Parse(c.PatientUhid), ct)
            ?? throw new InvalidOperationException($"Unknown patient '{c.PatientUhid}'.");
        return await _scheme.UpsertMembershipAsync(new SchemeMembership
        {
            PatientId = patient.PatientId, SchemeType = c.SchemeType, MemberNo = c.MemberNo,
            SecondaryRef = c.SecondaryRef, Verified = true
        }, ct);
    }
}

public sealed record SchemePackageDto(string Code, string Name, decimal Rate);
public sealed record GetSchemePackagesQuery(string SchemeType, string? Q) : IQuery<IReadOnlyList<SchemePackageDto>>;

public sealed class GetSchemePackagesHandler : MediatR.IRequestHandler<GetSchemePackagesQuery, IReadOnlyList<SchemePackageDto>>
{
    private readonly ISchemeRepository _scheme;
    public GetSchemePackagesHandler(ISchemeRepository scheme) { _scheme = scheme; }

    public async Task<IReadOnlyList<SchemePackageDto>> Handle(GetSchemePackagesQuery q, CancellationToken ct)
    {
        var rows = await _scheme.GetPackagesAsync(q.SchemeType, q.Q, ct);
        return rows.Select(p => new SchemePackageDto(p.Code, p.Name, p.Rate)).ToList();
    }
}
