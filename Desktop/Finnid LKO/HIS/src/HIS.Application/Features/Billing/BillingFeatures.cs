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
        RuleFor(x => x.Lines).NotEmpty().WithMessage("At least one charge line is required.");
        RuleFor(x => x.DiscountAmount).GreaterThanOrEqualTo(0);
        RuleFor(x => x.InsurancePays).GreaterThanOrEqualTo(0);
    }
}

public sealed class CreateBillHandler : MediatR.IRequestHandler<CreateBillCommand, CreateBillResult>
{
    private readonly IBillingRepository _billing;
    private readonly IPatientRepository _patients;
    private readonly IBranchContext _ctx;

    public CreateBillHandler(IBillingRepository billing, IPatientRepository patients, IBranchContext ctx)
    { _billing = billing; _patients = patients; _ctx = ctx; }

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
        return new CreateBillResult(id, billNo, gross, patientPays);
    }
}

// ============================ Get bill ============================
public sealed record BillLineViewDto(string Description, decimal Qty, decimal Rate, decimal Amount);
public sealed record BillDto(long BillId, string BillNo, decimal Gross, decimal Discount, decimal InsurancePays, decimal PatientPays, string Status, IReadOnlyList<BillLineViewDto> Lines);

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
        return new BillDto(bill.BillId, bill.BillNo, bill.GrossAmount, bill.DiscountAmount, bill.InsurancePays,
            bill.PatientPays, bill.Status,
            lines.Select(l => new BillLineViewDto(l.Description, l.Qty, l.Rate, l.Amount)).ToList());
    }
}
