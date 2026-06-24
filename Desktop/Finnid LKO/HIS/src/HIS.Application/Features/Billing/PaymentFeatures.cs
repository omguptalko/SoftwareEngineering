using FluentValidation;
using HIS.Application.Abstractions;
using HIS.Application.Features.Appointments;   // LookupCode
using HIS.Domain.Entities;
using HIS.Shared.Context;

namespace HIS.Application.Features.Billing;

// ============================ Collect payment (SRS §5) ============================
public sealed record CollectPaymentCommand(long? BillId, string PatientUhid, string Mode, decimal Amount)
    : ICommand<CollectPaymentResult>, IAuditable
{
    public string AuditEntity => "Payment";
    public string? AuditEntityId => BillId?.ToString() ?? PatientUhid;
}
public sealed record CollectPaymentResult(long PaymentId, string Provider, string Reference, string Status, bool BillSettled);

public sealed class CollectPaymentValidator : AbstractValidator<CollectPaymentCommand>
{
    public CollectPaymentValidator()
    {
        RuleFor(x => x.PatientUhid).NotEmpty();
        RuleFor(x => x.Mode).NotEmpty();
        RuleFor(x => x.Amount).GreaterThan(0);
    }
}

public sealed class CollectPaymentHandler : MediatR.IRequestHandler<CollectPaymentCommand, CollectPaymentResult>
{
    private readonly IBillingRepository _billing;
    private readonly IPatientRepository _patients;
    private readonly IPaymentGateway _gateway;

    public CollectPaymentHandler(IBillingRepository billing, IPatientRepository patients, IPaymentGateway gateway)
    { _billing = billing; _patients = patients; _gateway = gateway; }

    public async Task<CollectPaymentResult> Handle(CollectPaymentCommand c, CancellationToken ct)
    {
        var patient = await _patients.GetByUhidAsync(LookupCode.Parse(c.PatientUhid), ct)
            ?? throw new InvalidOperationException($"Unknown patient '{c.PatientUhid}'.");

        // Cash settles directly; everything else goes through the configured gateway.
        GatewayChargeResult charge = string.Equals(c.Mode, "Cash", StringComparison.OrdinalIgnoreCase)
            ? new GatewayChargeResult(true, "Cash", "CASH", "Captured")
            : await _gateway.ChargeAsync(new GatewayChargeRequest(c.Amount, c.Mode), ct);

        var paymentId = await _billing.InsertPaymentAsync(new Payment
        {
            BillId = c.BillId, PatientId = patient.PatientId, Mode = c.Mode,
            Gateway = charge.Provider, Amount = c.Amount, GatewayRef = charge.Reference,
            Status = charge.Success ? charge.Status : "Failed", CreatedUtc = DateTime.UtcNow
        }, ct);

        if (!charge.Success)
            throw new InvalidOperationException($"Payment failed at gateway '{charge.Provider}'.");

        var settled = false;
        if (c.BillId is long billId)
        {
            var bill = await _billing.GetBillAsync(billId, ct);
            var paid = await _billing.GetPaidTotalAsync(billId, ct);
            if (bill is not null && paid >= bill.PatientPays)
            {
                await _billing.UpdateBillStatusAsync(billId, "Paid", ct);
                settled = true;
            }
        }

        return new CollectPaymentResult(paymentId, charge.Provider, charge.Reference, charge.Status, settled);
    }
}

// ============================ Patient deposit top-up (SRS §5) ============================
public sealed record AddDepositCommand(string PatientUhid, decimal Amount, string Mode)
    : ICommand<AddDepositResult>, IAuditable
{
    public string AuditEntity => "PatientDeposit";
    public string? AuditEntityId => PatientUhid;
}
public sealed record AddDepositResult(long DepositId, decimal Balance, string Reference);

public sealed class AddDepositValidator : AbstractValidator<AddDepositCommand>
{
    public AddDepositValidator()
    {
        RuleFor(x => x.PatientUhid).NotEmpty();
        RuleFor(x => x.Amount).GreaterThan(0);
    }
}

public sealed class AddDepositHandler : MediatR.IRequestHandler<AddDepositCommand, AddDepositResult>
{
    private readonly IBillingRepository _billing;
    private readonly IPatientRepository _patients;
    private readonly IPaymentGateway _gateway;

    public AddDepositHandler(IBillingRepository billing, IPatientRepository patients, IPaymentGateway gateway)
    { _billing = billing; _patients = patients; _gateway = gateway; }

    public async Task<AddDepositResult> Handle(AddDepositCommand c, CancellationToken ct)
    {
        var patient = await _patients.GetByUhidAsync(LookupCode.Parse(c.PatientUhid), ct)
            ?? throw new InvalidOperationException($"Unknown patient '{c.PatientUhid}'.");

        GatewayChargeResult charge = string.Equals(c.Mode, "Cash", StringComparison.OrdinalIgnoreCase)
            ? new GatewayChargeResult(true, "Cash", "CASH", "Captured")
            : await _gateway.ChargeAsync(new GatewayChargeRequest(c.Amount, c.Mode), ct);
        if (!charge.Success) throw new InvalidOperationException("Deposit payment failed.");

        var balance = await _billing.GetDepositBalanceAsync(patient.PatientId, ct) + c.Amount;
        var id = await _billing.InsertDepositAsync(new PatientDeposit
        {
            PatientId = patient.PatientId, Amount = c.Amount, Balance = balance, CreatedUtc = DateTime.UtcNow
        }, ct);
        return new AddDepositResult(id, balance, charge.Reference);
    }
}
