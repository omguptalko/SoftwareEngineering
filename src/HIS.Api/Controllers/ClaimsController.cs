using HIS.Application.Features.Insurance;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace HIS.Api.Controllers;

[ApiController]
[Route("api/claims")]
public sealed class ClaimsController : ControllerBase
{
    private readonly IMediator _mediator;
    public ClaimsController(IMediator mediator) => _mediator = mediator;

    [HttpPost("policies")]
    public Task<long> CapturePolicy([FromBody] CapturePolicyCommand cmd, CancellationToken ct) => _mediator.Send(cmd, ct);

    [HttpGet("eligibility")]
    public Task<IReadOnlyList<PolicyDto>> Eligibility([FromQuery] string patientUhid, CancellationToken ct) => _mediator.Send(new GetEligibilityQuery(patientUhid), ct);

    [HttpPost("preauth")]
    public Task<CreatePreAuthResult> PreAuth([FromBody] CreatePreAuthCommand cmd, CancellationToken ct) => _mediator.Send(cmd, ct);

    [HttpPost("{claimId:long}/events")]
    public Task<string> Update(long claimId, [FromBody] ClaimEventBody body, CancellationToken ct)
        => _mediator.Send(new UpdateClaimStatusCommand(claimId, body.EventType, body.Amount, body.Notes), ct);

    [HttpGet("{claimId:long}")]
    public async Task<ActionResult<ClaimDto>> Get(long claimId, CancellationToken ct)
    {
        var dto = await _mediator.Send(new GetClaimQuery(claimId), ct);
        return dto is null ? NotFound() : Ok(dto);
    }

    [HttpGet("mis")]
    public Task<ClaimsMisDto> Mis(CancellationToken ct) => _mediator.Send(new GetClaimsMisQuery(), ct);

    [HttpPost("reconcile")]
    public Task<ReconcileResult> Reconcile([FromBody] ReconcileSettlementCommand cmd, CancellationToken ct) => _mediator.Send(cmd, ct);

    public sealed record ClaimEventBody(string EventType, decimal? Amount, string? Notes);
}

[ApiController]
[Route("api/pmjay")]
public sealed class PmjayController : ControllerBase
{
    private readonly IMediator _mediator;
    public PmjayController(IMediator mediator) => _mediator = mediator;

    [HttpPost("verify")]
    public Task<VerifyPmjayResult> Verify([FromBody] VerifyPmjayBeneficiaryCommand cmd, CancellationToken ct) => _mediator.Send(cmd, ct);

    [HttpPost("claim")]
    public Task<CreatePmjayClaimResult> Claim([FromBody] CreatePmjayClaimCommand cmd, CancellationToken ct) => _mediator.Send(cmd, ct);

    /// <summary>Submitted TMS claims (newest first) for the branch.</summary>
    [HttpGet("cases")]
    public Task<IReadOnlyList<PmjayCaseRowDto>> Cases(CancellationToken ct) => _mediator.Send(new GetPmjayCasesQuery(), ct);
}

[ApiController]
[Route("api/schemes")]
public sealed class SchemesController : ControllerBase
{
    private readonly IMediator _mediator;
    public SchemesController(IMediator mediator) => _mediator = mediator;

    [HttpPost("verify")]
    public Task<long> Verify([FromBody] VerifySchemeMembershipCommand cmd, CancellationToken ct) => _mediator.Send(cmd, ct);

    [HttpGet("packages")]
    public Task<IReadOnlyList<SchemePackageDto>> Packages([FromQuery] string schemeType, [FromQuery] string? q, CancellationToken ct)
        => _mediator.Send(new GetSchemePackagesQuery(schemeType, q), ct);

    /// <summary>Verified memberships for a scheme type (newest first).</summary>
    [HttpGet("memberships")]
    public Task<IReadOnlyList<SchemeMembershipRowDto>> Memberships([FromQuery] string schemeType, CancellationToken ct)
        => _mediator.Send(new GetSchemeMembershipsQuery(schemeType), ct);
}
