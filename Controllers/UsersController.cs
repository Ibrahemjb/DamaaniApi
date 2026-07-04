using DammaniAPI.Features.Users;
using DammaniAPI.Middlewares.Authentication;
using DammaniAPI.Utilities;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace DammaniAPI.Controllers;

[ApiController]
[Route("users")]
public class UsersController : ControllerBase
{
    private readonly IMediator _mediator;

    public UsersController(IMediator mediator) => _mediator = mediator;

    [HttpGet("checkIfUserExists")]
    public async Task<IActionResult> CheckIfUserExists([FromQuery] CheckIfUserExists.Query query)
        => Ok(await _mediator.Send(query));

    [Authorize]
    [HttpPost("updateProfile")]
    public async Task<IActionResult> UpdateProfile([FromBody] UpdateProfile.Command command)
    {
        command.UserId = HttpContext.CurrentUserId();
        return Ok(await _mediator.Send(command));
    }

    [Authorize]
    [HttpPost("changePassword")]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePassword.Command command)
    {
        command.UserId = HttpContext.CurrentUserId();
        return Ok(await _mediator.Send(command));
    }
}
