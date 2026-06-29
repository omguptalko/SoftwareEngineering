using HIS.Api.RealTime;
using HIS.Application.Features.Support;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;

namespace HIS.Api.Controllers;

[ApiController]
[Route("api/ambulance")]
public sealed class AmbulanceController : ControllerBase
{
    private readonly IMediator _m; public AmbulanceController(IMediator m) => _m = m;
    [HttpGet] public Task<IReadOnlyList<AmbulanceDto>> List(CancellationToken ct) => _m.Send(new GetAmbulancesQuery(), ct);
    [HttpGet("dispatches")] public Task<IReadOnlyList<DispatchRowDto>> Dispatches(CancellationToken ct) => _m.Send(new GetDispatchesQuery(), ct);
    [HttpPost("dispatch")] public Task<DispatchAmbulanceResult> Dispatch([FromBody] DispatchAmbulanceCommand cmd, CancellationToken ct) => _m.Send(cmd, ct);
    [HttpPost("dispatches/{id:long}/arrive")] public Task<bool> Arrive(long id, [FromBody] ArriveBody? b, CancellationToken ct) => _m.Send(new ArriveDispatchCommand(id, b?.Lat, b?.Lng), ct);
    public sealed record ArriveBody(decimal? Lat, decimal? Lng);
}

[ApiController]
[Route("api/diet")]
public sealed class DietController : ControllerBase
{
    private readonly IMediator _m; public DietController(IMediator m) => _m = m;
    [HttpGet] public Task<IReadOnlyList<DietRowDto>> List(CancellationToken ct) => _m.Send(new GetDietQuery(), ct);
    [HttpPost] public Task<long> Order([FromBody] OrderDietCommand cmd, CancellationToken ct) => _m.Send(cmd, ct);
}

[ApiController]
[Route("api/bmwm")]
public sealed class BmwmController : ControllerBase
{
    private readonly IMediator _m; public BmwmController(IMediator m) => _m = m;
    [HttpGet] public Task<BmwmDto> Get(CancellationToken ct) => _m.Send(new GetBmwmQuery(), ct);
    [HttpPost("bags")] public Task<long> Generate([FromBody] GenerateWasteBagCommand cmd, CancellationToken ct) => _m.Send(cmd, ct);
    [HttpPost("bags/{id:long}/handover")] public Task<bool> Handover(long id, CancellationToken ct) => _m.Send(new HandoverWasteBagCommand(id), ct);
}

[ApiController]
[Route("api/mortuary")]
public sealed class MortuaryController : ControllerBase
{
    private readonly IMediator _m; public MortuaryController(IMediator m) => _m = m;
    [HttpGet] public Task<IReadOnlyList<MortuaryRowDto>> List(CancellationToken ct) => _m.Send(new GetMortuaryQuery(), ct);
    [HttpPost("admit")] public Task<long> Admit([FromBody] AdmitBodyCommand cmd, CancellationToken ct) => _m.Send(cmd, ct);
    [HttpPost("{id:long}/release")] public Task<bool> Release(long id, CancellationToken ct) => _m.Send(new ReleaseBodyCommand(id), ct);
}

[ApiController]
[Route("api/mlc")]
public sealed class MlcController : ControllerBase
{
    private readonly IMediator _m; public MlcController(IMediator m) => _m = m;
    [HttpGet] public Task<IReadOnlyList<MlcRowDto>> List(CancellationToken ct) => _m.Send(new GetMlcQuery(), ct);
    [HttpPost] public Task<CreateMlcResult> Create([FromBody] CreateMlcCommand cmd, CancellationToken ct) => _m.Send(cmd, ct);
    [HttpPost("{id:long}/intimate")] public Task<bool> Intimate(long id, [FromBody] IntimateBody b, CancellationToken ct) => _m.Send(new IntimatePoliceCommand(id, b.AckRef), ct);
    public sealed record IntimateBody(string AckRef);
}

[ApiController]
[Route("api/consent")]
public sealed class ConsentController : ControllerBase
{
    private readonly IMediator _m; public ConsentController(IMediator m) => _m = m;
    [HttpGet("templates")] public Task<IReadOnlyList<ConsentTemplateDto>> Templates(CancellationToken ct) => _m.Send(new GetConsentTemplatesQuery(), ct);
    [HttpPost] public Task<long> Capture([FromBody] CaptureConsentCommand cmd, CancellationToken ct) => _m.Send(cmd, ct);
}

[ApiController]
[Route("api/certificates")]
public sealed class CertificatesController : ControllerBase
{
    private readonly IMediator _m; public CertificatesController(IMediator m) => _m = m;
    [HttpGet("templates")] public Task<IReadOnlyList<CertTemplateDto>> Templates(CancellationToken ct) => _m.Send(new GetCertTemplatesQuery(), ct);
    [HttpGet] public Task<IReadOnlyList<CertRowDto>> List(CancellationToken ct) => _m.Send(new GetCertificatesQuery(), ct);
    [HttpPost] public Task<long> Issue([FromBody] IssueCertificateCommand cmd, CancellationToken ct) => _m.Send(cmd, ct);
    [HttpPost("{id:long}/approve")] public Task<bool> Approve(long id, [FromBody] ApproveBody b, CancellationToken ct) => _m.Send(new ApproveCertificateCommand(id, b.DoctorCode), ct);
    public sealed record ApproveBody(string DoctorCode);
}

[ApiController]
[Route("api/feedback")]
public sealed class FeedbackController : ControllerBase
{
    private readonly IMediator _m; public FeedbackController(IMediator m) => _m = m;
    [HttpGet("grievances")] public Task<IReadOnlyList<GrievanceRowDto>> Grievances(CancellationToken ct) => _m.Send(new GetGrievancesQuery(), ct);
    [HttpPost("survey")] public Task<long> Survey([FromBody] SubmitFeedbackCommand cmd, CancellationToken ct) => _m.Send(cmd, ct);
    [HttpPost("grievances")] public Task<long> Log([FromBody] LogGrievanceCommand cmd, CancellationToken ct) => _m.Send(cmd, ct);
    [HttpPost("grievances/{id:long}/resolve")] public Task<bool> Resolve(long id, [FromBody] ResolveBody b, CancellationToken ct) => _m.Send(new ResolveGrievanceCommand(id, b.TatMinutes), ct);
    public sealed record ResolveBody(int TatMinutes);
}

[ApiController]
[Route("api/queue")]
public sealed class QueueController : ControllerBase
{
    private readonly IMediator _m;
    private readonly IHubContext<QueueHub> _hub;
    public QueueController(IMediator m, IHubContext<QueueHub> hub) { _m = m; _hub = hub; }

    [HttpGet("counters")] public Task<IReadOnlyList<CounterDto>> Counters(CancellationToken ct) => _m.Send(new GetCountersQuery(), ct);
    [HttpGet] public Task<IReadOnlyList<QueueRowDto>> Board(CancellationToken ct) => _m.Send(new GetQueueQuery(), ct);

    [HttpPost("counters/{id:int}/token")]
    public async Task<string> Issue(int id, [FromBody] IssueBody? b, CancellationToken ct)
    {
        var token = await _m.Send(new IssueTokenCommand(id, b?.PatientUhid), ct);
        await _hub.Clients.All.SendAsync("queueChanged", new { counterId = id, action = "issued", token }, ct);
        return token;
    }

    [HttpPost("counters/{id:int}/call-next")]
    public async Task<string?> CallNext(int id, CancellationToken ct)
    {
        var token = await _m.Send(new CallNextCommand(id), ct);
        await _hub.Clients.All.SendAsync("queueChanged", new { counterId = id, action = "called", token }, ct);
        return token;
    }
    public sealed record IssueBody(string? PatientUhid);
}
