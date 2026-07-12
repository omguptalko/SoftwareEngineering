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

    /// <summary>Branch directory for this tenant (Multi-Branch Sync console).</summary>
    [HttpGet("branches")]
    public Task<IReadOnlyList<BranchRowDto>> Branches(CancellationToken ct) => _mediator.Send(new GetBranchesQuery(), ct);

    /// <summary>Create or update a drug (gated by 'masters.manage').</summary>
    [HttpPost("drugs")]
    public Task<int> SaveDrug([FromBody] SaveDrugCommand cmd, CancellationToken ct) => _mediator.Send(cmd, ct);

    /// <summary>Deactivate / restore a drug (soft delete — keeps stock history).</summary>
    [HttpPost("drugs/set-active")]
    public Task<bool> SetActive([FromBody] SetDrugActiveCommand cmd, CancellationToken ct) => _mediator.Send(cmd, ct);

    /// <summary>Doctor master list (all doctors, including deactivated).</summary>
    [HttpGet("doctors")]
    public Task<IReadOnlyList<DoctorDto>> Doctors(CancellationToken ct) => _mediator.Send(new GetDoctorsAdminQuery(), ct);

    /// <summary>Create or update a doctor (gated by 'masters.manage').</summary>
    [HttpPost("doctors")]
    public Task<int> SaveDoctor([FromBody] SaveDoctorCommand cmd, CancellationToken ct) => _mediator.Send(cmd, ct);

    /// <summary>Deactivate / restore a doctor (soft delete — keeps clinical history).</summary>
    [HttpPost("doctors/set-active")]
    public Task<bool> SetDoctorActive([FromBody] SetDoctorActiveCommand cmd, CancellationToken ct) => _mediator.Send(cmd, ct);
}
