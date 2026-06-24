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

    [HttpGet("bills/{billId:long}")]
    public async Task<ActionResult<BillDto>> GetBill(long billId, CancellationToken ct)
    {
        var dto = await _mediator.Send(new GetBillQuery(billId), ct);
        return dto is null ? NotFound() : Ok(dto);
    }
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
}
