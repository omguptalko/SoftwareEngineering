using FluentValidation;
using HIS.Application.Abstractions;
using HIS.Application.Features.Appointments;   // LookupCode
using HIS.Domain.Entities;
using HIS.Shared.Context;

namespace HIS.Application.Features.Diagnostics;

// ============================ Radiology (SRS §3.9) ============================
public sealed record CreateRadiologyOrderCommand(string PatientUhid, string Modality, string? StudyName, bool IsPcPndtRegulated)
    : ICommand<long>, IAuditable
{
    public string AuditEntity => "RadiologyOrder";
    public string? AuditEntityId => PatientUhid;
}

public sealed class CreateRadiologyOrderValidator : AbstractValidator<CreateRadiologyOrderCommand>
{
    public CreateRadiologyOrderValidator()
    {
        RuleFor(x => x.PatientUhid).NotEmpty();
        RuleFor(x => x.Modality).NotEmpty();
    }
}

public sealed class CreateRadiologyOrderHandler : MediatR.IRequestHandler<CreateRadiologyOrderCommand, long>
{
    private readonly IRadiologyRepository _rad;
    private readonly IPatientRepository _patients;
    public CreateRadiologyOrderHandler(IRadiologyRepository rad, IPatientRepository patients) { _rad = rad; _patients = patients; }

    public async Task<long> Handle(CreateRadiologyOrderCommand c, CancellationToken ct)
    {
        var patient = await _patients.GetByUhidAsync(LookupCode.Parse(c.PatientUhid), ct)
            ?? throw new InvalidOperationException($"Unknown patient '{c.PatientUhid}'.");
        return await _rad.CreateOrderAsync(new RadiologyOrder
        {
            PatientId = patient.PatientId, Modality = c.Modality, StudyName = c.StudyName,
            ScheduledUtc = DateTime.UtcNow, IsPcPndtRegulated = c.IsPcPndtRegulated, Status = "Scheduled"
        }, ct);
    }
}

public sealed record RadWorklistItemDto(long RadOrderId, string Modality, string? Study, string Patient, string Status);
public sealed record GetRadiologyWorklistQuery : IQuery<IReadOnlyList<RadWorklistItemDto>>;

/// <summary>File a radiology report against an order (Scheduled -> Reported). SRS §3.9.</summary>
public sealed record ReportRadiologyCommand(long RadOrderId, string? ReportUrl)
    : ICommand<bool>, IAuditable
{
    public string AuditEntity => "RadiologyOrder";
    public string? AuditEntityId => RadOrderId.ToString();
}

public sealed class ReportRadiologyHandler : MediatR.IRequestHandler<ReportRadiologyCommand, bool>
{
    private readonly IRadiologyRepository _rad;
    public ReportRadiologyHandler(IRadiologyRepository rad) => _rad = rad;

    public async Task<bool> Handle(ReportRadiologyCommand c, CancellationToken ct)
    {
        await _rad.SetReportAsync(c.RadOrderId, "Reported", c.ReportUrl, ct);
        return true;
    }
}

public sealed class GetRadiologyWorklistHandler : MediatR.IRequestHandler<GetRadiologyWorklistQuery, IReadOnlyList<RadWorklistItemDto>>
{
    private readonly IRadiologyRepository _rad;
    private readonly IBranchContext _ctx;
    public GetRadiologyWorklistHandler(IRadiologyRepository rad, IBranchContext ctx) { _rad = rad; _ctx = ctx; }

    public async Task<IReadOnlyList<RadWorklistItemDto>> Handle(GetRadiologyWorklistQuery q, CancellationToken ct)
    {
        var rows = await _rad.GetWorklistAsync(_ctx.BranchId ?? 0, ct);
        return rows.Select(r => new RadWorklistItemDto(r.RadOrderId, r.Modality, r.Study, r.Patient, r.Status)).ToList();
    }
}

// ============================ Blood Bank (SRS §3.7) ============================
public sealed record BloodStockDto(string BloodGroup, int Units, int SafetyThreshold, bool BelowThreshold);
public sealed record GetBloodStockQuery : IQuery<IReadOnlyList<BloodStockDto>>;

public sealed class GetBloodStockHandler : MediatR.IRequestHandler<GetBloodStockQuery, IReadOnlyList<BloodStockDto>>
{
    private readonly IBloodBankRepository _bb;
    private readonly IBranchContext _ctx;
    public GetBloodStockHandler(IBloodBankRepository bb, IBranchContext ctx) { _bb = bb; _ctx = ctx; }

    public async Task<IReadOnlyList<BloodStockDto>> Handle(GetBloodStockQuery q, CancellationToken ct)
    {
        var rows = await _bb.GetStockAsync(_ctx.BranchId ?? 0, ct);
        return rows.Select(s => new BloodStockDto(s.BloodGroup, s.Units, s.SafetyThreshold, s.Units <= s.SafetyThreshold)).ToList();
    }
}

// ---- Add stock (donation / receipt) ----
public sealed record AddBloodStockCommand(string BloodGroup, int Units) : ICommand<bool>, IAuditable
{
    public string AuditEntity => "BloodStock";
    public string? AuditEntityId => BloodGroup;
}
public sealed class AddBloodStockValidator : AbstractValidator<AddBloodStockCommand>
{
    public AddBloodStockValidator()
    {
        RuleFor(x => x.BloodGroup).NotEmpty();
        RuleFor(x => x.Units).GreaterThan(0);
    }
}
public sealed class AddBloodStockHandler : MediatR.IRequestHandler<AddBloodStockCommand, bool>
{
    private readonly IBloodBankRepository _bb;
    private readonly IBranchContext _ctx;
    public AddBloodStockHandler(IBloodBankRepository bb, IBranchContext ctx) { _bb = bb; _ctx = ctx; }
    public async Task<bool> Handle(AddBloodStockCommand c, CancellationToken ct)
    {
        var branchId = _ctx.BranchId ?? throw new InvalidOperationException("Branch context required.");
        await _bb.AddStockAsync(branchId, c.BloodGroup, c.Units, ct);
        return true;
    }
}

// ---- Issue against a request (deduct stock + mark Fulfilled) ----
public sealed record IssueBloodCommand(long RequestId) : ICommand<bool>, IAuditable
{
    public string AuditEntity => "BloodRequest";
    public string? AuditEntityId => RequestId.ToString();
}
public sealed class IssueBloodHandler : MediatR.IRequestHandler<IssueBloodCommand, bool>
{
    private readonly IBloodBankRepository _bb;
    private readonly IBranchContext _ctx;
    public IssueBloodHandler(IBloodBankRepository bb, IBranchContext ctx) { _bb = bb; _ctx = ctx; }
    public async Task<bool> Handle(IssueBloodCommand c, CancellationToken ct)
    {
        var branchId = _ctx.BranchId ?? throw new InvalidOperationException("Branch context required.");
        var req = await _bb.GetRequestAsync(c.RequestId, ct)
            ?? throw new InvalidOperationException("Blood request not found.");
        if (string.Equals(req.Status, "Fulfilled", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Request already fulfilled.");

        var available = await _bb.GetAvailableUnitsAsync(branchId, req.BloodGroup, ct);
        if (available < req.Units)
            throw new InvalidOperationException($"Only {available} unit(s) of {req.BloodGroup} in stock; {req.Units} requested.");

        await _bb.AddStockAsync(branchId, req.BloodGroup, -req.Units, ct);   // deduct
        await _bb.SetRequestStatusAsync(c.RequestId, "Fulfilled", ct);
        return true;
    }
}

public sealed record BloodRequestRowDto(long RequestId, string? Patient, string BloodGroup, int Units, bool IsEmergency, string Status, DateTime RequestedUtc);
public sealed record GetBloodRequestsQuery : IQuery<IReadOnlyList<BloodRequestRowDto>>;

public sealed class GetBloodRequestsHandler : MediatR.IRequestHandler<GetBloodRequestsQuery, IReadOnlyList<BloodRequestRowDto>>
{
    private readonly IBloodBankRepository _bb;
    private readonly IBranchContext _ctx;
    public GetBloodRequestsHandler(IBloodBankRepository bb, IBranchContext ctx) { _bb = bb; _ctx = ctx; }

    public async Task<IReadOnlyList<BloodRequestRowDto>> Handle(GetBloodRequestsQuery q, CancellationToken ct)
    {
        var rows = await _bb.GetRequestsAsync(_ctx.BranchId ?? 0, ct);
        return rows.Select(r => new BloodRequestRowDto(r.RequestId, r.Patient, r.BloodGroup, r.Units, r.IsEmergency, r.Status, r.RequestedUtc)).ToList();
    }
}

public sealed record RaiseBloodRequestCommand(string? PatientUhid, string BloodGroup, int Units, bool IsEmergency)
    : ICommand<RaiseBloodRequestResult>, IAuditable
{
    public string AuditEntity => "BloodRequest";
    public string? AuditEntityId => BloodGroup;
}
public sealed record RaiseBloodRequestResult(long RequestId, bool DonorAlert);

public sealed class RaiseBloodRequestValidator : AbstractValidator<RaiseBloodRequestCommand>
{
    public RaiseBloodRequestValidator()
    {
        RuleFor(x => x.BloodGroup).NotEmpty();
        RuleFor(x => x.Units).GreaterThan(0);
    }
}

public sealed class RaiseBloodRequestHandler : MediatR.IRequestHandler<RaiseBloodRequestCommand, RaiseBloodRequestResult>
{
    private readonly IBloodBankRepository _bb;
    private readonly IPatientRepository _patients;
    private readonly IBranchContext _ctx;
    public RaiseBloodRequestHandler(IBloodBankRepository bb, IPatientRepository patients, IBranchContext ctx)
    { _bb = bb; _patients = patients; _ctx = ctx; }

    public async Task<RaiseBloodRequestResult> Handle(RaiseBloodRequestCommand c, CancellationToken ct)
    {
        var branchId = _ctx.BranchId ?? throw new InvalidOperationException("Branch context required.");
        long? patientId = null;
        if (!string.IsNullOrWhiteSpace(c.PatientUhid))
            patientId = (await _patients.GetByUhidAsync(LookupCode.Parse(c.PatientUhid!), ct))?.PatientId;

        var available = await _bb.GetAvailableUnitsAsync(branchId, c.BloodGroup, ct);
        var id = await _bb.CreateRequestAsync(new BloodRequest
        {
            BranchId = branchId, PatientId = patientId, BloodGroup = c.BloodGroup,
            Units = c.Units, IsEmergency = c.IsEmergency, RequestedUtc = DateTime.UtcNow, Status = "Requested"
        }, ct);

        // Donor alert when the request cannot be met from current stock (SRS §3.7).
        return new RaiseBloodRequestResult(id, available < c.Units);
    }
}
