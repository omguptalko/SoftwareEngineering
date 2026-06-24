using HIS.Application.Features.Dashboard;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace HIS.Api.Controllers;

[ApiController]
[Route("api/dashboard")]
public sealed class DashboardController : ControllerBase
{
    private readonly IMediator _mediator;
    public DashboardController(IMediator mediator) => _mediator = mediator;

    /// <summary>Admin dashboard KPIs + service activity (replaces hardcoded HTML in modules.js).</summary>
    [HttpGet]
    public Task<DashboardDto> Get(CancellationToken ct) => _mediator.Send(new GetDashboardQuery(), ct);
}
