using HIS.Application.Features.OccHealth;
using HIS.Application.Features.Telemedicine;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace HIS.Api.Controllers;

[ApiController]
[Route("api/occhealth")]
public sealed class OccHealthController : ControllerBase
{
    private readonly IMediator _mediator;
    public OccHealthController(IMediator mediator) => _mediator = mediator;

    [HttpGet("contracts")]
    public Task<IReadOnlyList<ContractDto>> Contracts(CancellationToken ct) => _mediator.Send(new GetContractsQuery(), ct);

    [HttpPost("contracts")]
    public Task<int> AddContract([FromBody] AddCompanyContractCommand cmd, CancellationToken ct) => _mediator.Send(cmd, ct);

    [HttpGet("exams")]
    public Task<IReadOnlyList<MedicalExamRowDto>> Exams(CancellationToken ct) => _mediator.Send(new GetMedicalExamsQuery(), ct);

    [HttpPost("exams")]
    public Task<long> ConductExam([FromBody] ConductMedicalExamCommand cmd, CancellationToken ct) => _mediator.Send(cmd, ct);

    [HttpPost("hazards")]
    public Task<long> RecordHazard([FromBody] RecordHazardCommand cmd, CancellationToken ct) => _mediator.Send(cmd, ct);

    [HttpGet("injuries")]
    public Task<IReadOnlyList<InjuryRowDto>> Injuries(CancellationToken ct) => _mediator.Send(new GetInjuriesQuery(), ct);

    [HttpPost("injuries")]
    public Task<long> RecordInjury([FromBody] RecordInjuryCommand cmd, CancellationToken ct) => _mediator.Send(cmd, ct);
}

[ApiController]
[Route("api/telemedicine")]
public sealed class TelemedicineController : ControllerBase
{
    private readonly IMediator _mediator;
    public TelemedicineController(IMediator mediator) => _mediator = mediator;

    [HttpGet]
    public Task<IReadOnlyList<TeleRowDto>> List(CancellationToken ct) => _mediator.Send(new GetTeleConsultsQuery(), ct);

    [HttpPost]
    public Task<long> Schedule([FromBody] ScheduleTeleConsultCommand cmd, CancellationToken ct) => _mediator.Send(cmd, ct);

    [HttpPost("{teleId:long}/consent")]
    public Task<bool> Consent(long teleId, CancellationToken ct) => _mediator.Send(new CaptureTeleConsentCommand(teleId), ct);

    [HttpPost("{teleId:long}/sign")]
    public Task<bool> Sign(long teleId, CancellationToken ct) => _mediator.Send(new SignEPrescriptionCommand(teleId), ct);

    [HttpPost("{teleId:long}/complete")]
    public Task<bool> Complete(long teleId, [FromBody] CompleteBody? body, CancellationToken ct)
        => _mediator.Send(new CompleteTeleConsultCommand(teleId, body?.SessionAuditUrl), ct);

    public sealed record CompleteBody(string? SessionAuditUrl);
}
