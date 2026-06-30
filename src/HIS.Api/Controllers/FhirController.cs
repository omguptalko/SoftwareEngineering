using System.Text.Json;
using HIS.Application.Features.Fhir;
using HIS.Shared.Fhir;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace HIS.Api.Controllers;

/// <summary>
/// HL7 FHIR R4 interoperability surface (SRS §8.6, task 0.10). Serves FHIR resources
/// with the conformant <c>application/fhir+json</c> media type. The HIP export reused by
/// ABDM record exchange (Phase 1) and NHCX claims (Phase 7).
/// </summary>
[ApiController]
[Route("api/fhir")]
public sealed class FhirController : ControllerBase
{
    private readonly IMediator _mediator;
    public FhirController(IMediator mediator) => _mediator = mediator;

    /// <summary>FHIR R4 Patient resource for a UHID (auth required, tenant-scoped).</summary>
    [HttpGet("Patient/{uhid}")]
    public async Task<IActionResult> Patient(string uhid, CancellationToken ct)
    {
        var resource = await _mediator.Send(new GetFhirPatientQuery(uhid), ct);
        if (resource is null) return NotFound();
        var json = JsonSerializer.Serialize(resource, FhirJson.Options);
        return Content(json, FhirJson.MediaType);
    }
}
