using HIS.Application.Features.Masters;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace HIS.Api.Controllers;

[ApiController]
[Route("api/masters")]
public sealed class MastersController : ControllerBase
{
    private readonly IMediator _mediator;
    public MastersController(IMediator mediator) => _mediator = mediator;

    /// <summary>Drug master list (all drugs, including deactivated).</summary>
    [HttpGet("drugs")]
    public Task<IReadOnlyList<DrugDto>> Drugs(CancellationToken ct) => _mediator.Send(new GetDrugsAdminQuery(), ct);

    /// <summary>Create or update a drug (gated by 'masters.manage').</summary>
    [HttpPost("drugs")]
    public Task<int> SaveDrug([FromBody] SaveDrugCommand cmd, CancellationToken ct) => _mediator.Send(cmd, ct);

    /// <summary>Deactivate / restore a drug (soft delete — keeps stock history).</summary>
    [HttpPost("drugs/set-active")]
    public Task<bool> SetActive([FromBody] SetDrugActiveCommand cmd, CancellationToken ct) => _mediator.Send(cmd, ct);
}
