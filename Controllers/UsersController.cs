using DammaniAPI.Features.Users;
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
}
