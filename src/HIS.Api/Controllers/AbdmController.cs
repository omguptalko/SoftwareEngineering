using HIS.Application.Features.Abdm;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace HIS.Api.Controllers;

/// <summary>
/// ABDM / ABHA Console (SRS §6.2) — consent artifacts (HIP/HIU) and HFR/HPR
/// registry onboarding. Auth + tenant-scoped; every write is audited.
/// </summary>
[ApiController]
[Route("api/abdm")]
public sealed class AbdmController : ControllerBase
{
    private readonly IMediator _mediator;
    public AbdmController(IMediator mediator) => _mediator = mediator;

    // ---- Consent artifacts ----
    [HttpGet("consents")]
    public Task<IReadOnlyList<ConsentRowDto>> Consents(CancellationToken ct)
        => _mediator.Send(new GetAbdmConsentsQuery(), ct);

    [HttpPost("consents")]
    public async Task<IActionResult> RequestConsent([FromBody] RequestConsentCommand cmd, CancellationToken ct)
        => Ok(new { consentArtifactId = await _mediator.Send(cmd, ct) });

    [HttpPost("consents/{id:long}/grant")]
    public async Task<IActionResult> Grant(long id, [FromBody] GrantConsentRequest? body, CancellationToken ct)
        => Ok(new { ok = await _mediator.Send(new SetConsentStatusCommand(id, "grant", body?.ValidityMonths), ct) });

    [HttpPost("consents/{id:long}/revoke")]
    public async Task<IActionResult> Revoke(long id, CancellationToken ct)
        => Ok(new { ok = await _mediator.Send(new SetConsentStatusCommand(id, "revoke", null), ct) });

    // ---- HFR facilities ----
    [HttpGet("facilities")]
    public Task<IReadOnlyList<FacilityRowDto>> Facilities(CancellationToken ct)
        => _mediator.Send(new GetHfrFacilitiesQuery(), ct);

    [HttpPost("facilities")]
    public async Task<IActionResult> OnboardFacility([FromBody] OnboardFacilityCommand cmd, CancellationToken ct)
        => Ok(new { hfrId = await _mediator.Send(cmd, ct) });

    // ---- HPR professionals ----
    [HttpGet("professionals")]
    public Task<IReadOnlyList<ProfessionalRowDto>> Professionals(CancellationToken ct)
        => _mediator.Send(new GetHprProfessionalsQuery(), ct);

    [HttpPost("professionals")]
    public async Task<IActionResult> OnboardProfessional([FromBody] OnboardProfessionalCommand cmd, CancellationToken ct)
        => Ok(new { hprId = await _mediator.Send(cmd, ct) });
}

public sealed record GrantConsentRequest(int? ValidityMonths);
