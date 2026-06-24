using HIS.Application.Features.Diagnostics;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace HIS.Api.Controllers;

[ApiController]
[Route("api/lab")]
public sealed class LabController : ControllerBase
{
    private readonly IMediator _mediator;
    public LabController(IMediator mediator) => _mediator = mediator;

    [HttpGet("worklist")]
    public Task<IReadOnlyList<LabWorklistItemDto>> Worklist(CancellationToken ct) => _mediator.Send(new GetLabWorklistQuery(), ct);

    [HttpPost("orders")]
    public Task<CreateLabOrderResult> CreateOrder([FromBody] CreateLabOrderCommand cmd, CancellationToken ct) => _mediator.Send(cmd, ct);

    [HttpPost("results")]
    public Task<bool> EnterResults([FromBody] EnterLabResultsCommand cmd, CancellationToken ct) => _mediator.Send(cmd, ct);
}

[ApiController]
[Route("api/radiology")]
public sealed class RadiologyController : ControllerBase
{
    private readonly IMediator _mediator;
    public RadiologyController(IMediator mediator) => _mediator = mediator;

    [HttpGet("worklist")]
    public Task<IReadOnlyList<RadWorklistItemDto>> Worklist(CancellationToken ct) => _mediator.Send(new GetRadiologyWorklistQuery(), ct);

    [HttpPost("orders")]
    public Task<long> CreateOrder([FromBody] CreateRadiologyOrderCommand cmd, CancellationToken ct) => _mediator.Send(cmd, ct);
}

[ApiController]
[Route("api/bloodbank")]
public sealed class BloodBankController : ControllerBase
{
    private readonly IMediator _mediator;
    public BloodBankController(IMediator mediator) => _mediator = mediator;

    [HttpGet("stock")]
    public Task<IReadOnlyList<BloodStockDto>> Stock(CancellationToken ct) => _mediator.Send(new GetBloodStockQuery(), ct);

    [HttpPost("requests")]
    public Task<RaiseBloodRequestResult> RaiseRequest([FromBody] RaiseBloodRequestCommand cmd, CancellationToken ct) => _mediator.Send(cmd, ct);
}
