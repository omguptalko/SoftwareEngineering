using FluentValidation;
using HIS.Application.Abstractions;
using HIS.Application.Features.Appointments;   // LookupCode
using HIS.Domain.Entities;
using HIS.Shared.Context;
using Microsoft.Extensions.Configuration;

namespace HIS.Application.Features.Pharmacy;

// ---- Prescription queue (SRS §3.10) ----
public sealed record RxQueueItemDto(long PrescriptionId, string Patient, string Doctor, int Items, string Status);
public sealed record GetPrescriptionQueueQuery : IQuery<IReadOnlyList<RxQueueItemDto>>;

public sealed class GetPrescriptionQueueHandler : MediatR.IRequestHandler<GetPrescriptionQueueQuery, IReadOnlyList<RxQueueItemDto>>
{
    private readonly IPharmacyRepository _ph;
    private readonly IBranchContext _ctx;
    public GetPrescriptionQueueHandler(IPharmacyRepository ph, IBranchContext ctx) { _ph = ph; _ctx = ctx; }

    public async Task<IReadOnlyList<RxQueueItemDto>> Handle(GetPrescriptionQueueQuery q, CancellationToken ct)
    {
        var rows = await _ph.GetQueueAsync(_ctx.BranchId ?? 0, ct);
        return rows.Select(r => new RxQueueItemDto(r.PrescriptionId, r.Patient, r.Doctor, r.Items, r.Status)).ToList();
    }
}

// ---- Batches for a drug (lookup) ----
public sealed record DrugBatchDto(string BatchNo, string Expiry, decimal Mrp, int QtyOnHand);
public sealed record GetDrugBatchesQuery(string DrugCode) : IQuery<IReadOnlyList<DrugBatchDto>>;

public sealed class GetDrugBatchesHandler : MediatR.IRequestHandler<GetDrugBatchesQuery, IReadOnlyList<DrugBatchDto>>
{
    private readonly IPharmacyRepository _ph;
    public GetDrugBatchesHandler(IPharmacyRepository ph) { _ph = ph; }

    public async Task<IReadOnlyList<DrugBatchDto>> Handle(GetDrugBatchesQuery q, CancellationToken ct)
    {
        var drugId = await _ph.GetDrugIdByCodeAsync(LookupCode.Parse(q.DrugCode), ct);
        if (drugId is null) return Array.Empty<DrugBatchDto>();
        var rows = await _ph.GetBatchesAsync(drugId.Value, ct);
        return rows.Select(b => new DrugBatchDto(b.BatchNo, b.Expiry, b.Mrp, b.QtyOnHand)).ToList();
    }
}

// ---- Dispense (SRS §3.10) — batch/expiry validated, stock auto-deducted ----
public sealed record DispenseLineDto(string DrugCode, string BatchNo, int Qty);

public sealed record DispenseCommand(long? PrescriptionId, bool IsNdps, IReadOnlyList<DispenseLineDto> Lines)
    : ICommand<DispenseResult>, IAuditable
{
    public string AuditEntity => "Dispense";
    public string? AuditEntityId => PrescriptionId?.ToString();
}
public sealed record DispenseResult(long DispenseId, decimal Total);

public sealed class DispenseValidator : AbstractValidator<DispenseCommand>
{
    public DispenseValidator()
    {
        RuleFor(x => x.Lines).NotEmpty().WithMessage("At least one item is required.");
        RuleForEach(x => x.Lines).ChildRules(l =>
        {
            l.RuleFor(x => x.DrugCode).NotEmpty();
            l.RuleFor(x => x.BatchNo).NotEmpty();
            l.RuleFor(x => x.Qty).GreaterThan(0);
        });
    }
}

public sealed class DispenseHandler : MediatR.IRequestHandler<DispenseCommand, DispenseResult>
{
    private readonly IPharmacyRepository _ph;
    private readonly IBranchContext _ctx;
    private readonly IConfiguration _config;

    public DispenseHandler(IPharmacyRepository ph, IBranchContext ctx, IConfiguration config)
    { _ph = ph; _ctx = ctx; _config = config; }

    public async Task<DispenseResult> Handle(DispenseCommand c, CancellationToken ct)
    {
        var branchId = _ctx.BranchId ?? throw new InvalidOperationException("Branch context required.");
        // Block dispensing batches expiring within this many days (config-driven, not hardcoded).
        var expiryBlockDays = _config.GetValue("Pharmacy:ExpiryBlockDays", 0);

        var lines = new List<DispenseLineInput>();
        foreach (var l in c.Lines)
        {
            var drugId = await _ph.GetDrugIdByCodeAsync(LookupCode.Parse(l.DrugCode), ct)
                ?? throw new InvalidOperationException($"Unknown drug '{l.DrugCode}'.");
            lines.Add(new DispenseLineInput(drugId, l.BatchNo.Trim(), l.Qty));
        }

        var dispense = new Dispense
        {
            PrescriptionId = c.PrescriptionId,
            BranchId = branchId,
            DispensedUtc = DateTime.UtcNow,
            IsNdps = c.IsNdps
        };

        var (id, total) = await _ph.DispenseAsync(dispense, lines, expiryBlockDays, ct);
        return new DispenseResult(id, total);
    }
}
