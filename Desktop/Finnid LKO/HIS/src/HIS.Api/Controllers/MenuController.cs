using HIS.Application.Features.Platform;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace HIS.Api.Controllers;

[ApiController]
[Route("api/menu")]
public sealed class MenuController : ControllerBase
{
    private readonly IMediator _mediator;
    public MenuController(IMediator mediator) => _mediator = mediator;

    /// <summary>Effective module/page menu for the current authenticated user (L1.3.5).</summary>
    [HttpGet]
    public Task<IReadOnlyList<MenuModuleDto>> MyMenu(CancellationToken ct) => _mediator.Send(new GetMyMenuQuery(), ct);
}
