using FluentValidation;
using HIS.Application.Abstractions;
using HIS.Application.Features.Appointments;   // LookupCode
using HIS.Domain.Entities;
using HIS.Shared.Context;
using Microsoft.Extensions.Configuration;

namespace HIS.Application.Features.Insurance;

// ============================ Capture policy + eligibility (SRS §3.15) ============================
public sealed record CapturePolicyCommand(
    string PatientUhid, string PayerCode, string? PolicyNo, string? MemberId,
    decimal? SumInsured, decimal? RoomRentCapPerDay, decimal? CoPayPct, DateTime? ValidTo)
    : ICommand<long>, IAuditable
{
    public string AuditEntity => "InsurancePolicy";
    public string? AuditEntityId => PatientUhid;
}

public sealed class CapturePolicyValidator : AbstractValidator<CapturePolicyCommand>
{
    public CapturePolicyValidator()
    {
        RuleFor(x => x.PatientUhid).NotEmpty();
        RuleFor(x => x.PayerCode).NotEmpty();
    }
}

public sealed class CapturePolicyHandler : MediatR.IRequestHandler<CapturePolicyCommand, long>
{
    private readonly IClaimsRepository _claims;
    private readonly IPatientRepository _patients;
    public CapturePolicyHandler(IClaimsRepository claims, IPatientRepository patients) { _claims = claims; _patients = patients; }

    public async Task<long> Handle(CapturePolicyCommand c, CancellationToken ct)
    {
        var patient = await _patients.GetByUhidAsync(LookupCode.Parse(c.PatientUhid), ct)
            ?? throw new InvalidOperationException($"Unknown patient '{c.PatientUhid}'.");
        var payerId = await _claims.GetPayerIdByCodeAsync(LookupCode.Parse(c.PayerCode), ct)
            ?? throw new InvalidOperationException($"Unknown payer '{c.PayerCode}'.");

        return await _claims.InsertPolicyAsync(new InsurancePolicy
        {
            PatientId = patient.PatientId, PayerId = payerId, PolicyNo = c.PolicyNo, MemberId = c.MemberId,
            SumInsured = c.SumInsured, AvailableBalance = c.SumInsured,    // opening balance = sum insured
            RoomRentCapPerDay = c.RoomRentCapPerDay, CoPayPct = c.CoPayPct, ValidTo = c.ValidTo
        }, ct);
    }
}

public sealed record PolicyDto(long PolicyId, string Payer, string? PolicyNo, decimal? SumInsured, decimal? AvailableBalance, decimal? RoomRentCap, decimal? CoPayPct);
public sealed record GetEligibilityQuery(string PatientUhid) : IQuery<IReadOnlyList<PolicyDto>>;

public sealed class GetEligibilityHandler : MediatR.IRequestHandler<GetEligibilityQuery, IReadOnlyList<PolicyDto>>
{
    private readonly IClaimsRepository _claims;
    private readonly IPatientRepository _patients;
    public GetEligibilityHandler(IClaimsRepository claims, IPatientRepository patients) { _claims = claims; _patients = patients; }

    public async Task<IReadOnlyList<PolicyDto>> Handle(GetEligibilityQuery q, CancellationToken ct)
    {
        var patient = await _patients.GetByUhidAsync(LookupCode.Parse(q.PatientUhid), ct);
        if (patient is null) return Array.Empty<PolicyDto>();
        var rows = await _claims.GetPoliciesAsync(patient.PatientId, ct);
        return rows.Select(r => new PolicyDto(r.PolicyId, r.Payer, r.PolicyNo, r.SumInsured, r.AvailableBalance, r.RoomRentCap, r.CoPayPct)).ToList();
    }
}

// ============================ Pre-authorisation (SRS §7.1) ============================
public sealed record CreatePreAuthCommand(
    string PatientUhid, string PayerCode, long? PolicyId, long? AdmissionId,
    string? ProvisionalIcd10, decimal PreAuthAmount, string? Channel, IReadOnlyList<string>? MandatoryDocs)
    : ICommand<CreatePreAuthResult>, IAuditable
{
    public string AuditEntity => "Claim";
    public string? AuditEntityId => PatientUhid;
}
public sealed record CreatePreAuthResult(long ClaimId, string ClaimNo, DateTime TatDueUtc);

public sealed class CreatePreAuthValidator : AbstractValidator<CreatePreAuthCommand>
{
    public CreatePreAuthValidator()
    {
        RuleFor(x => x.PatientUhid).NotEmpty();
        RuleFor(x => x.PayerCode).NotEmpty();
        RuleFor(x => x.PreAuthAmount).GreaterThan(0);
    }
}

public sealed class CreatePreAuthHandler : MediatR.IRequestHandler<CreatePreAuthCommand, CreatePreAuthResult>
{
    private readonly IClaimsRepository _claims;
    private readonly IPatientRepository _patients;
    private readonly IBranchContext _ctx;
    private readonly IConfiguration _config;

    public CreatePreAuthHandler(IClaimsRepository claims, IPatientRepository patients, IBranchContext ctx, IConfiguration config)
    { _claims = claims; _patients = patients; _ctx = ctx; _config = config; }

    public async Task<CreatePreAuthResult> Handle(CreatePreAuthCommand c, CancellationToken ct)
    {
        var branchId = _ctx.BranchId ?? throw new InvalidOperationException("Branch context required.");
        var patient = await _patients.GetByUhidAsync(LookupCode.Parse(c.PatientUhid), ct)
            ?? throw new InvalidOperationException($"Unknown patient '{c.PatientUhid}'.");
        var payerId = await _claims.GetPayerIdByCodeAsync(LookupCode.Parse(c.PayerCode), ct)
            ?? throw new InvalidOperationException($"Unknown payer '{c.PayerCode}'.");

        var tatHours = _config.GetValue("Claims:TatHours", 24);   // TAT clock from config, not hardcoded
        var now = DateTime.UtcNow;
        var tatDue = now.AddHours(tatHours);

        var claimNo = await _claims.NextClaimNoAsync(branchId, ct);
        var claimId = await _claims.InsertClaimAsync(new Claim
        {
            ClaimNo = claimNo, BranchId = branchId, PatientId = patient.PatientId, PayerId = payerId,
            PolicyId = c.PolicyId, AdmissionId = c.AdmissionId, Channel = c.Channel ?? "NHCX",
            ProvisionalIcd10 = LookupCode.Parse(c.ProvisionalIcd10 ?? ""), PreAuthAmount = c.PreAuthAmount,
            Status = "PreAuth", SubmittedUtc = now, TatDueUtc = tatDue
        }, ct);

        await _claims.AddEventAsync(new ClaimEvent { ClaimId = claimId, EventType = "PreAuth", Amount = c.PreAuthAmount, OccurredUtc = now }, ct);
        foreach (var d in c.MandatoryDocs ?? Array.Empty<string>())
            await _claims.AddDocumentAsync(new ClaimDocument { ClaimId = claimId, DocType = d, IsMandatory = true, Attached = true }, ct);

        return new CreatePreAuthResult(claimId, claimNo, tatDue);
    }
}

// ============================ Claim lifecycle transition (SRS §7.1) ============================
public sealed record UpdateClaimStatusCommand(long ClaimId, string EventType, decimal? Amount, string? Notes)
    : ICommand<string>, IAuditable
{
    public string AuditEntity => "Claim";
    public string? AuditEntityId => ClaimId.ToString();
}

public sealed class UpdateClaimStatusValidator : AbstractValidator<UpdateClaimStatusCommand>
{
    public UpdateClaimStatusValidator()
    {
        RuleFor(x => x.ClaimId).GreaterThan(0);
        RuleFor(x => x.EventType).NotEmpty()
            .Must(e => _allowed.Contains(e)).WithMessage("Unknown claim event type.");
    }
    private static readonly HashSet<string> _allowed = new(StringComparer.OrdinalIgnoreCase)
    { "Query", "Shortfall", "Enhancement", "FinalBill", "Approval", "Denial", "Settlement", "Appeal" };
}

public sealed class UpdateClaimStatusHandler : MediatR.IRequestHandler<UpdateClaimStatusCommand, string>
{
    private readonly IClaimsRepository _claims;
    public UpdateClaimStatusHandler(IClaimsRepository claims) { _claims = claims; }

    public async Task<string> Handle(UpdateClaimStatusCommand c, CancellationToken ct)
    {
        var claim = await _claims.GetClaimAsync(c.ClaimId, ct)
            ?? throw new InvalidOperationException("Claim not found.");

        // Map the lifecycle event to the resulting claim status (SRS §7.1).
        claim.Status = c.EventType.ToLowerInvariant() switch
        {
            "query"       => "Query",
            "shortfall"   => "Shortfall",
            "enhancement" => "Enhancement",
            "finalbill"   => "FinalBill",
            "approval"    => "Approved",
            "denial"      => "Denied",
            "settlement"  => "Settled",
            _             => claim.Status        // Appeal keeps current status
        };
        if (c.EventType.Equals("Approval", StringComparison.OrdinalIgnoreCase)) claim.ApprovedAmount = c.Amount;
        if (c.EventType.Equals("Settlement", StringComparison.OrdinalIgnoreCase)) claim.SettledAmount = c.Amount;

        await _claims.UpdateClaimAsync(claim, ct);
        await _claims.AddEventAsync(new ClaimEvent { ClaimId = c.ClaimId, EventType = c.EventType, Amount = c.Amount, Notes = c.Notes, OccurredUtc = DateTime.UtcNow }, ct);
        return claim.Status;
    }
}

// ============================ Get claim + MIS dashboard (SRS §7.8) ============================
public sealed record ClaimEventDto(string EventType, decimal? Amount, string? Notes, string OccurredUtc);
public sealed record ClaimDto(long ClaimId, string ClaimNo, string Channel, string Status, decimal? PreAuth, decimal? Approved, decimal? Settled, string? TatDueUtc, IReadOnlyList<ClaimEventDto> Events);

public sealed record GetClaimQuery(long ClaimId) : IQuery<ClaimDto?>;

public sealed class GetClaimHandler : MediatR.IRequestHandler<GetClaimQuery, ClaimDto?>
{
    private readonly IClaimsRepository _claims;
    public GetClaimHandler(IClaimsRepository claims) { _claims = claims; }

    public async Task<ClaimDto?> Handle(GetClaimQuery q, CancellationToken ct)
    {
        var claim = await _claims.GetClaimAsync(q.ClaimId, ct);
        if (claim is null) return null;
        var events = await _claims.GetEventsAsync(q.ClaimId, ct);
        return new ClaimDto(claim.ClaimId, claim.ClaimNo, claim.Channel ?? "", claim.Status,
            claim.PreAuthAmount, claim.ApprovedAmount, claim.SettledAmount, claim.TatDueUtc?.ToString("u"),
            events.Select(e => new ClaimEventDto(e.EventType, e.Amount, e.Notes, e.OccurredUtc.ToString("u"))).ToList());
    }
}

public sealed record ClaimRowDto(long ClaimId, string ClaimNo, string Patient, string Payer, decimal? PreAuth, decimal? Approved, string Status, string? SubmittedUtc);
public sealed record StatusCountDto(string Status, int Count);
public sealed record ClaimsMisDto(IReadOnlyList<StatusCountDto> Counts, IReadOnlyList<ClaimRowDto> Claims);

public sealed record GetClaimsMisQuery : IQuery<ClaimsMisDto>;

public sealed class GetClaimsMisHandler : MediatR.IRequestHandler<GetClaimsMisQuery, ClaimsMisDto>
{
    private readonly IClaimsRepository _claims;
    private readonly IBranchContext _ctx;
    public GetClaimsMisHandler(IClaimsRepository claims, IBranchContext ctx) { _claims = claims; _ctx = ctx; }

    public async Task<ClaimsMisDto> Handle(GetClaimsMisQuery q, CancellationToken ct)
    {
        var branchId = _ctx.BranchId ?? 0;
        var counts = await _claims.GetStatusCountsAsync(branchId, ct);
        var rows = await _claims.GetClaimsAsync(branchId, ct);
        return new ClaimsMisDto(
            counts.Select(x => new StatusCountDto(x.Status, x.Count)).ToList(),
            rows.Select(r => new ClaimRowDto(r.ClaimId, r.ClaimNo, r.Patient, r.Payer, r.PreAuth, r.Approved, r.Status, r.SubmittedUtc?.ToString("yyyy-MM-dd"))).ToList());
    }
}

// ============================ Settlement reconciliation (SRS §7.8) ============================
public sealed record ReconcileSettlementCommand(long ClaimId, string Utr, decimal BankAmount)
    : ICommand<ReconcileResult>, IAuditable
{
    public string AuditEntity => "SettlementReconciliation";
    public string? AuditEntityId => ClaimId.ToString();
}
public sealed record ReconcileResult(long ReconId, string Status, bool Matched);

public sealed class ReconcileSettlementValidator : AbstractValidator<ReconcileSettlementCommand>
{
    public ReconcileSettlementValidator()
    {
        RuleFor(x => x.ClaimId).GreaterThan(0);
        RuleFor(x => x.Utr).NotEmpty();
        RuleFor(x => x.BankAmount).GreaterThan(0);
    }
}

public sealed class ReconcileSettlementHandler : MediatR.IRequestHandler<ReconcileSettlementCommand, ReconcileResult>
{
    private readonly IClaimsRepository _claims;
    public ReconcileSettlementHandler(IClaimsRepository claims) { _claims = claims; }

    public async Task<ReconcileResult> Handle(ReconcileSettlementCommand c, CancellationToken ct)
    {
        var claim = await _claims.GetClaimAsync(c.ClaimId, ct) ?? throw new InvalidOperationException("Claim not found.");
        var matched = claim.SettledAmount.HasValue && claim.SettledAmount.Value == c.BankAmount;
        var status = matched ? "Matched" : "Mismatch";

        var reconId = await _claims.InsertReconciliationAsync(new SettlementReconciliation
        {
            ClaimId = c.ClaimId, Utr = c.Utr, BankAmount = c.BankAmount, ReconciledUtc = DateTime.UtcNow, Status = status
        }, ct);
        return new ReconcileResult(reconId, status, matched);
    }
}
