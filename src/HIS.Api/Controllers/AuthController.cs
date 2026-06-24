using HIS.Application.Features.Auth;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace HIS.Api.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController : ControllerBase
{
    private readonly IMediator _mediator;
    public AuthController(IMediator mediator) => _mediator = mediator;

    /// <summary>Authenticate a control-plane user and receive a JWT (L1.2).</summary>
    [HttpPost("login")]
    public async Task<ActionResult<LoginResult>> Login([FromBody] LoginCommand cmd, CancellationToken ct)
    {
        try
        {
            var result = await _mediator.Send(cmd, ct);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            // Invalid credentials → 401 (ValidationException for empty fields still bubbles to 400).
            return Unauthorized(new { title = ex.Message });
        }
    }
}
