using HIS.Application.Features.Lookups;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace HIS.Api.Controllers;

[ApiController]
[Route("api/lookups")]
public sealed class LookupsController : ControllerBase
{
    private readonly IMediator _mediator;
    public LookupsController(IMediator mediator) => _mediator = mediator;

    /// <summary>F3 lookup feed (doctor/drug/icd10/ward/payer/patient/package) — was static.</summary>
    [HttpGet("{type}")]
    public Task<LookupResultDto> Get(string type, [FromQuery] string? q, CancellationToken ct) =>
        _mediator.Send(new GetLookupQuery(type, q), ct);
}
