using HIS.Api.RealTime;
using HIS.Application.Features.Icu;
using HIS.Shared.Context;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;

namespace HIS.Api.Controllers;

[ApiController]
[Route("api/icu")]
public sealed class IcuController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IHubContext<AlertsHub> _alerts;
    private readonly ITenantContext _tenant;
    public IcuController(IMediator mediator, IHubContext<AlertsHub> alerts, ITenantContext tenant)
    { _mediator = mediator; _alerts = alerts; _tenant = tenant; }

    /// <summary>ICU census — patients currently admitted in an ICU/HDU/critical-care ward (SRS §3.6).</summary>
    [HttpGet("admissions")]
    public Task<IReadOnlyList<IcuAdmissionDto>> Admissions(CancellationToken ct) => _mediator.Send(new GetIcuAdmissionsQuery(), ct);

    /// <summary>ICU monitoring flowsheet for an admission (most recent first).</summary>
    [HttpGet("admissions/{admissionId:long}/observations")]
    public Task<IReadOnlyList<IcuObsDto>> Flowsheet(long admissionId, CancellationToken ct) =>
        _mediator.Send(new GetIcuFlowsheetQuery(admissionId), ct);

    /// <summary>Record one ICU observation (flowsheet entry); broadcasts a live monitoring update.</summary>
    [HttpPost("admissions/{admissionId:long}/observations")]
    public async Task<long> Record(long admissionId, [FromBody] RecordIcuObsCommand body, CancellationToken ct)
    {
        var cmd = body with { AdmissionId = admissionId };
        var id = await _mediator.Send(cmd, ct);
        await _alerts.Clients.Group(TenantGroups.Name(_tenant)).SendAsync("icuChanged", new { admissionId, observationId = id }, ct);
        return id;
    }
}
