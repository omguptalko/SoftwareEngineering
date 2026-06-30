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
}
