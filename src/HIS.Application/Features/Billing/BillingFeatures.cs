using FluentValidation;
using HIS.Application.Abstractions;
using HIS.Application.Features.Appointments;   // LookupCode
using HIS.Domain.Entities;
using HIS.Shared.Context;

namespace HIS.Application.Features.Billing;

// ============================ Create bill (SRS §3.14) ============================
public sealed record BillLineDto(string? TariffCode, string Description, decimal Qty, decimal Rate);

public sealed record CreateBillCommand(
    string PatientUhid, long? AdmissionId, decimal DiscountAmount, decimal InsurancePays, IReadOnlyList<BillLineDto> Lines)
    : ICommand<CreateBillResult>, IAuditable
{
    public string AuditEntity => "Bill";
    public string? AuditEntityId => PatientUhid;
}
public sealed record CreateBillResult(long BillId, string BillNo, decimal Gross, decimal PatientPays);

public sealed class CreateBillValidator : AbstractValidator<CreateBillCommand>
{
    public CreateBillValidator()
    {
        RuleFor(x => x.PatientUhid).NotEmpty();
        // Lines may be empty when the only charges are auto-accrued (e.g. a pure consultation
        // bill). The handler still rejects a bill that ends up with no lines at all.
        RuleFor(x => x.DiscountAmount).GreaterThanOrEqualTo(0);
        RuleFor(x => x.InsurancePays).GreaterThanOrEqualTo(0);
    }
}

public sealed class CreateBillHandler : MediatR.IRequestHandler<CreateBillCommand, CreateBillResult>
{
    private readonly IBillingRepository _billing;
    private readonly IPatientRepository _patients;
    private readonly IPendingChargeRepository _pending;
    private readonly IBranchContext _ctx;

    public CreateBillHandler(IBillingRepository billing, IPatientRepository patients, IPendingChargeRepository pending, IBranchContext ctx)
    { _billing = billing; _patients = patients; _pending = pending; _ctx = ctx; }

    public async Task<CreateBillResult> Handle(CreateBillCommand c, CancellationToken ct)
    {
        var branchId = _ctx.BranchId ?? throw new InvalidOperationException("Branch context required.");
        var patient = await _patients.GetByUhidAsync(LookupCode.Parse(c.PatientUhid), ct)
            ?? throw new InvalidOperationException($"Unknown patient '{c.PatientUhid}'.");

        var lines = new List<BillLine>();
        foreach (var l in c.Lines)
        {
            int? tariffId = null;
            var rate = l.Rate;
            var desc = l.Description;
            if (!string.IsNullOrWhiteSpace(l.TariffCode))
            {
                var tariff = await _billing.GetTariffByCodeAsync(branchId, LookupCode.Parse(l.TariffCode!), ct);
                if (tariff is not null)
                {
                    tariffId = tariff.TariffId;
                    if (rate <= 0) rate = tariff.Rate;                 // pull rate from master when not overridden
                    if (string.IsNullOrWhiteSpace(desc)) desc = tariff.ServiceName;
                }
            }
            lines.Add(new BillLine { TariffId = tariffId, Description = desc, Qty = l.Qty <= 0 ? 1 : l.Qty, Rate = rate });
        }

        // Billing Phase 2: auto-pull this patient's accrued-but-unbilled charges (doctor fees, etc.)
        // into the bill. Admission bills pull that admission's charges; plain bills pull OPD charges.
        var accrued = await _pending.GetUnbilledForBillAsync(patient.PatientId, c.AdmissionId, ct);
        foreach (var a in accrued)
            lines.Add(new BillLine { TariffId = a.TariffId, Description = a.Description, Qty = a.Qty, Rate = a.Rate });

        if (lines.Count == 0)
            throw new InvalidOperationException("No charges to bill — add a line or accrue a consultation/consultant fee first.");

        var gross = lines.Sum(l => l.Qty * l.Rate);
        var patientPays = Math.Max(0, gross - c.DiscountAmount - c.InsurancePays);

        var billNo = await _billing.NextBillNoAsync(branchId, ct);
        var bill = new Bill
        {
            BillNo = billNo, BranchId = branchId, PatientId = patient.PatientId, AdmissionId = c.AdmissionId,
            CreatedUtc = DateTime.UtcNow, GrossAmount = gross, DiscountAmount = c.DiscountAmount,
            InsurancePays = c.InsurancePays, PatientPays = patientPays, Status = "Open"
        };

        var id = await _billing.CreateBillAsync(bill, lines, ct);

        // Stamp the accrued charges as billed so they are never pulled into a second bill.
        if (accrued.Count > 0)
            await _pending.MarkBilledAsync(accrued.Select(a => a.ChargeId), id, ct);

        return new CreateBillResult(id, billNo, gross, patientPays);
    }
}

// ============================ Get bill ============================
public sealed record BillLineViewDto(string Description, decimal Qty, decimal Rate, decimal Amount);
public sealed record BillDto(long BillId, string BillNo, decimal Gross, decimal Discount, decimal InsurancePays, decimal PatientPays, string Status, string? PatientUhid, string? Patient, IReadOnlyList<BillLineViewDto> Lines);

public sealed record BillRowDto(long BillId, string BillNo, string Patient, decimal Gross, decimal PatientPays, decimal Paid, decimal Balance, string Status, string CreatedUtc);
public sealed record GetBillsQuery(string? Q = null, string? Status = null, DateTime? From = null, DateTime? To = null, int Take = 200) : IQuery<IReadOnlyList<BillRowDto>>;
public sealed class GetBillsHandler : MediatR.IRequestHandler<GetBillsQuery, IReadOnlyList<BillRowDto>>
{
    private readonly IBillingRepository _billing; private readonly IBranchContext _ctx;
    public GetBillsHandler(IBillingRepository billing, IBranchContext ctx) { _billing = billing; _ctx = ctx; }
    public async Task<IReadOnlyList<BillRowDto>> Handle(GetBillsQuery q, CancellationToken ct)
        => (await _billing.GetBillsAsync(_ctx.BranchId ?? 0, q.Q, q.Status, q.From, q.To, q.Take, ct)).Select(b => new BillRowDto(
            b.BillId, b.BillNo, b.Patient, b.Gross, b.PatientPays, b.Paid,
            b.PatientPays - b.Paid, b.Status, b.CreatedUtc.ToString("yyyy-MM-dd HH:mm"))).ToList();
}

public sealed record GetBillQuery(long BillId) : IQuery<BillDto?>;

public sealed class GetBillHandler : MediatR.IRequestHandler<GetBillQuery, BillDto?>
{
    private readonly IBillingRepository _billing;
    public GetBillHandler(IBillingRepository billing) { _billing = billing; }

    public async Task<BillDto?> Handle(GetBillQuery q, CancellationToken ct)
    {
        var bill = await _billing.GetBillAsync(q.BillId, ct);
        if (bill is null) return null;
        var lines = await _billing.GetBillLinesAsync(q.BillId, ct);
        var pref = await _billing.GetPatientRefAsync(bill.PatientId, ct);
        return new BillDto(bill.BillId, bill.BillNo, bill.GrossAmount, bill.DiscountAmount, bill.InsurancePays,
            bill.PatientPays, bill.Status, pref?.Uhid, pref?.FullName,
            lines.Select(l => new BillLineViewDto(l.Description, l.Qty, l.Rate, l.Amount)).ToList());
    }
}
