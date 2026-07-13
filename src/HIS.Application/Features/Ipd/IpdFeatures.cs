using FluentValidation;
using HIS.Application.Abstractions;
using HIS.Application.Features.Appointments;   // LookupCode
using HIS.Domain.Entities;
using HIS.Shared.Context;

namespace HIS.Application.Features.Ipd;

// ============================ Admit ============================
/// <summary>Admit a patient to a bed — SRS §3.4. Generates AdmissionNo, occupies the bed.</summary>
public sealed record AdmitPatientCommand(
    string PatientUhid, string BedLabel, string? ConsultantCode,
    string? ProvisionalIcd10, string? AdmissionType, string? PaymentClass, int? EstStayDays)
    : ICommand<AdmitPatientResult>, IAuditable
{
    public string AuditEntity => "Admission";
    public string? AuditEntityId => PatientUhid;
}
public sealed record AdmitPatientResult(long AdmissionId, string AdmissionNo, string BedNo);

public sealed class AdmitPatientValidator : AbstractValidator<AdmitPatientCommand>
{
    public AdmitPatientValidator()
    {
        RuleFor(x => x.PatientUhid).NotEmpty();
        RuleFor(x => x.BedLabel).NotEmpty();
    }
}

public sealed class AdmitPatientHandler : MediatR.IRequestHandler<AdmitPatientCommand, AdmitPatientResult>
{
    private readonly IAdmissionRepository _adm;
    private readonly IPatientRepository _patients;
    private readonly IPendingChargeRepository _pending;
    private readonly IBranchContext _ctx;

    public AdmitPatientHandler(IAdmissionRepository adm, IPatientRepository patients, IPendingChargeRepository pending, IBranchContext ctx)
    { _adm = adm; _patients = patients; _pending = pending; _ctx = ctx; }

    public async Task<AdmitPatientResult> Handle(AdmitPatientCommand c, CancellationToken ct)
    {
        var branchId = _ctx.BranchId ?? throw new InvalidOperationException("Branch context required.");
        var patient = await _patients.GetByUhidAsync(LookupCode.Parse(c.PatientUhid), ct)
            ?? throw new InvalidOperationException($"Unknown patient '{c.PatientUhid}'.");

        var bedNo = LookupCode.ParseTail(c.BedLabel);
        var bed = await _adm.GetBedByNoAsync(branchId, bedNo, ct)
            ?? throw new InvalidOperationException($"Unknown bed '{bedNo}'.");
        if (!string.Equals(bed.Status, "free", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"Bed '{bedNo}' is not available (status: {bed.Status}).");

        var consultantId = string.IsNullOrWhiteSpace(c.ConsultantCode)
            ? (int?)null : await _adm.GetDoctorIdByCodeAsync(LookupCode.Parse(c.ConsultantCode!), ct);

        var admissionNo = await _adm.NextAdmissionNoAsync(branchId, ct);
        var id = await _adm.InsertAsync(new Admission
        {
            AdmissionNo = admissionNo,
            BranchId = branchId,
            PatientId = patient.PatientId,
            BedId = bed.BedId,
            ConsultantId = consultantId,
            AdmittedUtc = DateTime.UtcNow,
            AdmissionType = c.AdmissionType,
            PaymentClass = c.PaymentClass,
            ProvisionalIcd10 = LookupCode.Parse(c.ProvisionalIcd10 ?? ""),
            EstStayDays = c.EstStayDays,
            Status = "Admitted"
        }, ct);

        await _adm.SetBedStatusAsync(bed.BedId, "occ", ct);

        // Billing Phase 2: accrue the consultant's fee against this admission (fee snapshotted now).
        // Pulled into the discharge bill later so it is never missed.
        if (consultantId is int feeConsultantId)
            await _pending.AccrueDoctorFeeAsync(branchId, patient.PatientId, id, feeConsultantId, "IPD Consultant", ct);

        return new AdmitPatientResult(id, admissionNo, bedNo);
    }
}

// ============================ Transfer ============================
public sealed record TransferBedCommand(long AdmissionId, string ToBedLabel, string? Reason)
    : ICommand<bool>, IAuditable
{
    public string AuditEntity => "Admission";
    public string? AuditEntityId => AdmissionId.ToString();
}

public sealed class TransferBedHandler : MediatR.IRequestHandler<TransferBedCommand, bool>
{
    private readonly IAdmissionRepository _adm;
    private readonly IBranchContext _ctx;
    public TransferBedHandler(IAdmissionRepository adm, IBranchContext ctx) { _adm = adm; _ctx = ctx; }

    public async Task<bool> Handle(TransferBedCommand c, CancellationToken ct)
    {
        var branchId = _ctx.BranchId ?? throw new InvalidOperationException("Branch context required.");
        var adm = await _adm.GetAsync(c.AdmissionId, ct)
            ?? throw new InvalidOperationException("Admission not found.");
        if (adm.Status != "Admitted") throw new InvalidOperationException("Admission is not active.");

        var bedNo = LookupCode.ParseTail(c.ToBedLabel);
        var toBed = await _adm.GetBedByNoAsync(branchId, bedNo, ct)
            ?? throw new InvalidOperationException($"Unknown bed '{bedNo}'.");
        if (!string.Equals(toBed.Status, "free", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"Bed '{bedNo}' is not available.");

        await _adm.InsertTransferAsync(new BedTransfer
        {
            AdmissionId = adm.AdmissionId, FromBedId = adm.BedId, ToBedId = toBed.BedId,
            TransferUtc = DateTime.UtcNow, Reason = c.Reason
        }, ct);

        if (adm.BedId is int oldBed) await _adm.SetBedStatusAsync(oldBed, "clean", ct);
        await _adm.SetBedStatusAsync(toBed.BedId, "occ", ct);
        await _adm.UpdateBedAsync(adm.AdmissionId, toBed.BedId, ct);
        return true;
    }
}

// ============================ Discharge ============================
public sealed record DischargePatientCommand(long AdmissionId, string? DischargeSummary)
    : ICommand<bool>, IAuditable
{
    public string AuditEntity => "Admission";
    public string? AuditEntityId => AdmissionId.ToString();
}

public sealed class DischargePatientHandler : MediatR.IRequestHandler<DischargePatientCommand, bool>
{
    private readonly IAdmissionRepository _adm;
    public DischargePatientHandler(IAdmissionRepository adm) { _adm = adm; }

    public async Task<bool> Handle(DischargePatientCommand c, CancellationToken ct)
    {
        var adm = await _adm.GetAsync(c.AdmissionId, ct)
            ?? throw new InvalidOperationException("Admission not found.");
        if (adm.Status == "Discharged") throw new InvalidOperationException("Already discharged.");

        await _adm.DischargeAsync(adm.AdmissionId, c.DischargeSummary, DateTime.UtcNow, ct);
        if (adm.BedId is int bed) await _adm.SetBedStatusAsync(bed, "clean", ct);
        return true;
    }
}

// ======================= Housekeeping: mark bed ready =======================
/// <summary>Housekeeping: return a cleaned bed to the available pool (clean -> free).
/// Closes the bed lifecycle so beds recycle after discharge (SRS §3.4). Without this,
/// a bed stuck in 'clean' can never be admitted into again.</summary>
public sealed record MarkBedReadyCommand(string BedLabel) : ICommand<bool>, IAuditable
{
    public string AuditEntity => "Bed";
    public string? AuditEntityId => BedLabel;
}

public sealed class MarkBedReadyValidator : AbstractValidator<MarkBedReadyCommand>
{
    public MarkBedReadyValidator() => RuleFor(x => x.BedLabel).NotEmpty();
}

public sealed class MarkBedReadyHandler : MediatR.IRequestHandler<MarkBedReadyCommand, bool>
{
    private readonly IAdmissionRepository _adm;
    private readonly IBranchContext _ctx;
    public MarkBedReadyHandler(IAdmissionRepository adm, IBranchContext ctx) { _adm = adm; _ctx = ctx; }

    public async Task<bool> Handle(MarkBedReadyCommand c, CancellationToken ct)
    {
        var branchId = _ctx.BranchId ?? throw new InvalidOperationException("Branch context required.");
        var bedNo = LookupCode.ParseTail(c.BedLabel);
        var bed = await _adm.GetBedByNoAsync(branchId, bedNo, ct)
            ?? throw new InvalidOperationException($"Unknown bed '{bedNo}'.");

        if (string.Equals(bed.Status, "free", StringComparison.OrdinalIgnoreCase))
            return false; // already available — idempotent no-op
        if (!string.Equals(bed.Status, "clean", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"Bed '{bedNo}' cannot be marked ready (status: {bed.Status}).");

        await _adm.SetBedStatusAsync(bed.BedId, "free", ct);
        return true;
    }
}

// ============================ Bed board ============================
public sealed record BedDto(string Ward, string BedNo, string Status, string? Occupant);
public sealed record GetBedBoardQuery : IQuery<IReadOnlyList<BedDto>>;

public sealed class GetBedBoardHandler : MediatR.IRequestHandler<GetBedBoardQuery, IReadOnlyList<BedDto>>
{
    private readonly IAdmissionRepository _adm;
    private readonly IBranchContext _ctx;
    public GetBedBoardHandler(IAdmissionRepository adm, IBranchContext ctx) { _adm = adm; _ctx = ctx; }

    public async Task<IReadOnlyList<BedDto>> Handle(GetBedBoardQuery q, CancellationToken ct)
    {
        var rows = await _adm.GetBedBoardAsync(_ctx.BranchId ?? 0, ct);
        return rows.Select(r => new BedDto(r.Ward, r.BedNo, r.Status, r.Occupant)).ToList();
    }
}

// ==================== Ward / bed management (dynamic, SRS §3.4) ====================
public sealed record WardDto(int WardId, string Name);
public sealed record GetWardsQuery : IQuery<IReadOnlyList<WardDto>>, IRequireAuthentication;

public sealed class GetWardsHandler : MediatR.IRequestHandler<GetWardsQuery, IReadOnlyList<WardDto>>
{
    private readonly IAdmissionRepository _adm;
    private readonly IBranchContext _ctx;
    public GetWardsHandler(IAdmissionRepository adm, IBranchContext ctx) { _adm = adm; _ctx = ctx; }
    public async Task<IReadOnlyList<WardDto>> Handle(GetWardsQuery q, CancellationToken ct)
        => (await _adm.GetWardsAsync(_ctx.BranchId ?? 0, ct)).Select(w => new WardDto(w.WardId, w.Name)).ToList();
}

public sealed record AddWardCommand(string Name) : ICommand<int>, IAuditable
{
    public string AuditEntity => "Ward";
    public string? AuditEntityId => Name;
}
public sealed class AddWardValidator : AbstractValidator<AddWardCommand>
{
    public AddWardValidator() => RuleFor(x => x.Name).NotEmpty().MaximumLength(80);
}
public sealed class AddWardHandler : MediatR.IRequestHandler<AddWardCommand, int>
{
    private readonly IAdmissionRepository _adm;
    private readonly IBranchContext _ctx;
    public AddWardHandler(IAdmissionRepository adm, IBranchContext ctx) { _adm = adm; _ctx = ctx; }
    public Task<int> Handle(AddWardCommand c, CancellationToken ct)
    {
        var branchId = _ctx.BranchId ?? throw new InvalidOperationException("Branch context required.");
        return _adm.AddWardAsync(branchId, c.Name.Trim(), ct);
    }
}

public sealed record AddBedCommand(int WardId, string BedNo) : ICommand<int>, IAuditable
{
    public string AuditEntity => "Bed";
    public string? AuditEntityId => BedNo;
}
public sealed class AddBedValidator : AbstractValidator<AddBedCommand>
{
    public AddBedValidator()
    {
        RuleFor(x => x.WardId).GreaterThan(0);
        RuleFor(x => x.BedNo).NotEmpty().MaximumLength(20);
    }
}
public sealed class AddBedHandler : MediatR.IRequestHandler<AddBedCommand, int>
{
    private readonly IAdmissionRepository _adm;
    private readonly IBranchContext _ctx;
    public AddBedHandler(IAdmissionRepository adm, IBranchContext ctx) { _adm = adm; _ctx = ctx; }
    public async Task<int> Handle(AddBedCommand c, CancellationToken ct)
    {
        var branchId = _ctx.BranchId ?? throw new InvalidOperationException("Branch context required.");
        // Ensure the ward is in the caller's branch (tenant/branch isolation).
        if (!await _adm.WardBelongsToBranchAsync(c.WardId, branchId, ct))
            throw new InvalidOperationException("Selected ward does not belong to this branch.");
        var bedNo = c.BedNo.Trim();
        // Bed numbers are unique within a branch.
        if (await _adm.GetBedByNoAsync(branchId, bedNo, ct) is not null)
            throw new InvalidOperationException($"Bed '{bedNo}' already exists in this branch.");
        return await _adm.AddBedAsync(c.WardId, bedNo, ct);
    }
}

// ==================== Admitted patients (who is in which bed) ====================
public sealed record AdmittedPatientDto(long AdmissionId, string AdmissionNo, string Patient, string Uhid, string Ward, string BedNo, string? Consultant, DateTime AdmittedUtc);
public sealed record GetAdmittedPatientsQuery : IQuery<IReadOnlyList<AdmittedPatientDto>>;

public sealed class GetAdmittedPatientsHandler : MediatR.IRequestHandler<GetAdmittedPatientsQuery, IReadOnlyList<AdmittedPatientDto>>
{
    private readonly IAdmissionRepository _adm;
    private readonly IBranchContext _ctx;
    public GetAdmittedPatientsHandler(IAdmissionRepository adm, IBranchContext ctx) { _adm = adm; _ctx = ctx; }

    public async Task<IReadOnlyList<AdmittedPatientDto>> Handle(GetAdmittedPatientsQuery q, CancellationToken ct)
    {
        var rows = await _adm.GetAdmittedPatientsAsync(_ctx.BranchId ?? 0, ct);
        return rows.Select(r => new AdmittedPatientDto(r.AdmissionId, r.AdmissionNo, r.Patient, r.Uhid, r.Ward, r.BedNo, r.Consultant, r.AdmittedUtc)).ToList();
    }
}
