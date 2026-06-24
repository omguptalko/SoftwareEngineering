using HIS.Application.Features.Meta;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace HIS.Api.Controllers;

[ApiController]
[Route("api/meta")]
public sealed class MetaController : ControllerBase
{
    private readonly IMediator _mediator;
    public MetaController(IMediator mediator) => _mediator = mediator;

    /// <summary>Module registry + groups for the sidebar (replaces static data.js arrays).</summary>
    [HttpGet("registry")]
    public Task<ModuleRegistryDto> Registry(CancellationToken ct) =>
        _mediator.Send(new GetModuleRegistryQuery(), ct);
}
