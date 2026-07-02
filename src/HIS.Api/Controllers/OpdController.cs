using HIS.Application.Features.Opd;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace HIS.Api.Controllers;

[ApiController]
[Route("api/opd")]
public sealed class OpdController : ControllerBase
{
    private readonly IMediator _mediator;
    public OpdController(IMediator mediator) => _mediator = mediator;

    /// <summary>This hospital's OPD department templates (extra clinical fields per specialty).</summary>
    [HttpGet("templates")]
    public Task<IReadOnlyList<OpdTemplateDto>> Templates(CancellationToken ct) =>
        _mediator.Send(new GetOpdTemplatesQuery(), ct);

    /// <summary>Replace a department's template fields (admin-configurable, gated by 'opd.templates.manage').</summary>
    [HttpPost("templates")]
    public Task<bool> SaveTemplate([FromBody] SaveOpdTemplateCommand cmd, CancellationToken ct) =>
        _mediator.Send(cmd, ct);
}
