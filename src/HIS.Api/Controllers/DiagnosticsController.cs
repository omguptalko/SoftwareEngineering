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

    /// <summary>File a report against a radiology order (Scheduled -> Reported).</summary>
    [HttpPost("report")]
    public Task<bool> Report([FromBody] ReportRadiologyCommand cmd, CancellationToken ct) => _mediator.Send(cmd, ct);
}

[ApiController]
[Route("api/bloodbank")]
public sealed class BloodBankController : ControllerBase
{
    private readonly IMediator _mediator;
    public BloodBankController(IMediator mediator) => _mediator = mediator;

    [HttpGet("stock")]
    public Task<IReadOnlyList<BloodStockDto>> Stock(CancellationToken ct) => _mediator.Send(new GetBloodStockQuery(), ct);

    /// <summary>Blood requests raised (newest first).</summary>
    [HttpGet("requests")]
    public Task<IReadOnlyList<BloodRequestRowDto>> Requests(CancellationToken ct) => _mediator.Send(new GetBloodRequestsQuery(), ct);

    [HttpPost("requests")]
    public Task<RaiseBloodRequestResult> RaiseRequest([FromBody] RaiseBloodRequestCommand cmd, CancellationToken ct) => _mediator.Send(cmd, ct);

    /// <summary>Add units to stock (donation / receipt).</summary>
    [HttpPost("stock/add")]
    public Task<bool> AddStock([FromBody] AddBloodStockCommand cmd, CancellationToken ct) => _mediator.Send(cmd, ct);

    /// <summary>Issue blood against a request — deducts stock and marks it Fulfilled.</summary>
    [HttpPost("requests/{id:long}/issue")]
    public Task<bool> Issue(long id, CancellationToken ct) => _mediator.Send(new IssueBloodCommand(id), ct);
}
