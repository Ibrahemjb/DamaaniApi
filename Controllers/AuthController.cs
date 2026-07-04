using DammaniAPI.Features.Auth;
using DammaniAPI.Middlewares.Authentication;
using DammaniAPI.Utilities;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace DammaniAPI.Controllers;

[ApiController]
[Route("auth")]
public class AuthController : ControllerBase
{
    private readonly IMediator _mediator;

    public AuthController(IMediator mediator) => _mediator = mediator;

    [HttpPost("signup")]
    public async Task<IActionResult> Signup([FromBody] Signup.Command command)
        => Ok(await _mediator.Send(command));

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] Login.Command command)
        => Ok(await _mediator.Send(command));

    [Authorize]
    [HttpGet("me")]
    public async Task<IActionResult> Me()
        => Ok(await _mediator.Send(new GetMe.Query { UserId = HttpContext.CurrentUserId() }));

    [HttpPost("requestPasswordReset")]
    public async Task<IActionResult> RequestPasswordReset([FromBody] RequestPasswordReset.Command command)
    {
        command.IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
        return Ok(await _mediator.Send(command));
    }

    [HttpPost("resetPassword")]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPassword.Command command)
        => Ok(await _mediator.Send(command));
}
