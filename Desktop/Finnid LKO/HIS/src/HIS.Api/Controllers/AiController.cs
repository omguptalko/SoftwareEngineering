using HIS.Application.Features.Ai;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace HIS.Api.Controllers;

/// <summary>
/// AI layer (SRS §4, Phase 11). Models are consumed via this API surface; the current
/// implementations are transparent, explainable baselines behind a config-swappable seam
/// (Azure AI / Python ML can replace a handler without changing this contract).
/// </summary>
[ApiController]
[Route("api/ai")]
public sealed class AiController : ControllerBase
{
    private readonly IMediator _mediator;
    public AiController(IMediator mediator) => _mediator = mediator;

    /// <summary>11.1 — patient risk / early-warning score from vitals (auth required).</summary>
    [HttpPost("risk")]
    public Task<RiskResult> Risk([FromBody] RiskVitalsInput vitals, CancellationToken ct)
        => _mediator.Send(new AssessPatientRiskQuery(vitals), ct);

    /// <summary>11.4 — inventory demand forecast + suggested reorder quantities (auth required).</summary>
    [HttpGet("inventory-forecast")]
    public Task<IReadOnlyList<ForecastRow>> InventoryForecast(CancellationToken ct)
        => _mediator.Send(new GetInventoryForecastQuery(), ct);

    /// <summary>11.6 — claim pre-scrubbing against payer/package rules before submission (auth required).</summary>
    [HttpPost("claim-prescrub")]
    public Task<PreScrubResult> PreScrub([FromBody] PreScrubInput input, CancellationToken ct)
        => _mediator.Send(new PreScrubClaimQuery(input), ct);
}
