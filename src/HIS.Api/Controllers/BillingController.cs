using HIS.Application.Features.Billing;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace HIS.Api.Controllers;

[ApiController]
[Route("api/billing")]
public sealed class BillingController : ControllerBase
{
    private readonly IMediator _mediator;
    public BillingController(IMediator mediator) => _mediator = mediator;

    [HttpPost("bills")]
    public Task<CreateBillResult> CreateBill([FromBody] CreateBillCommand cmd, CancellationToken ct) => _mediator.Send(cmd, ct);

    [HttpGet("bills")]
    public Task<IReadOnlyList<BillRowDto>> Bills(CancellationToken ct) => _mediator.Send(new GetBillsQuery(), ct);

    [HttpGet("bills/{billId:long}")]
    public async Task<ActionResult<BillDto>> GetBill(long billId, CancellationToken ct)
    {
        var dto = await _mediator.Send(new GetBillQuery(billId), ct);
        return dto is null ? NotFound() : Ok(dto);
    }

    /// <summary>Accrued-but-unbilled charges for a patient (auto-pulled into the next bill).</summary>
    [HttpGet("pending-charges")]
    public Task<IReadOnlyList<PendingChargeDto>> PendingCharges([FromQuery] string patientUhid, CancellationToken ct)
        => _mediator.Send(new GetPendingChargesQuery(patientUhid), ct);
}

[ApiController]
[Route("api/payments")]
public sealed class PaymentsController : ControllerBase
{
    private readonly IMediator _mediator;
    public PaymentsController(IMediator mediator) => _mediator = mediator;

    [HttpPost("collect")]
    public Task<CollectPaymentResult> Collect([FromBody] CollectPaymentCommand cmd, CancellationToken ct) => _mediator.Send(cmd, ct);

    [HttpPost("deposit")]
    public Task<AddDepositResult> Deposit([FromBody] AddDepositCommand cmd, CancellationToken ct) => _mediator.Send(cmd, ct);

    /// <summary>Payment Gateway console (§5) — configured provider / environment / modes.</summary>
    [HttpGet("gateway")]
    public Task<GatewayStatusDto> Gateway(CancellationToken ct) => _mediator.Send(new GetGatewayStatusQuery(), ct);

    /// <summary>Gateway transactions ledger (billing.Payment), newest first.</summary>
    [HttpGet]
    public Task<IReadOnlyList<GatewayTxnDto>> Transactions([FromQuery] int take = 100, CancellationToken ct = default)
        => _mediator.Send(new GetGatewayTransactionsQuery(take), ct);
}
