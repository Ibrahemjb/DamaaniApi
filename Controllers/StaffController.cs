using DammaniAPI.Features;
using DammaniAPI.Features.Staff;
using DammaniAPI.Middlewares.Authentication;
using DammaniAPI.Utilities;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace DammaniAPI.Controllers;

[ApiController]
[Route("staff")]
public class StaffController : ControllerBase
{
    private readonly IMediator _mediator;

    public StaffController(IMediator mediator) => _mediator = mediator;

    [Authorize(Roles.Owner)]
    [HttpGet("getStaff")]
    public async Task<IActionResult> GetStaff()
        => Ok(await _mediator.Send(new GetStaff.Query { ShopId = HttpContext.CurrentShopId() }));

    [Authorize(Roles.Owner)]
    [HttpPost("inviteStaff")]
    public async Task<IActionResult> InviteStaff([FromBody] InviteStaff.Command command)
    {
        command.ShopId = HttpContext.CurrentShopId();
        command.ActorUserId = HttpContext.CurrentUserId();
        return Ok(await _mediator.Send(command));
    }

    [Authorize(Roles.Owner)]
    [HttpPost("revokeInvite")]
    public async Task<IActionResult> RevokeInvite([FromBody] RevokeInvite.Command command)
    {
        command.ShopId = HttpContext.CurrentShopId();
        command.ActorUserId = HttpContext.CurrentUserId();
        return Ok(await _mediator.Send(command));
    }

    [Authorize(Roles.Owner)]
    [HttpPost("renameStaff")]
    public async Task<IActionResult> RenameStaff([FromBody] RenameStaff.Command command)
    {
        command.ShopId = HttpContext.CurrentShopId();
        command.ActorUserId = HttpContext.CurrentUserId();
        return Ok(await _mediator.Send(command));
    }

    [Authorize(Roles.Owner)]
    [HttpPost("setStaffStatus")]
    public async Task<IActionResult> SetStaffStatus([FromBody] SetStaffStatus.Command command)
    {
        command.ShopId = HttpContext.CurrentShopId();
        command.ActorUserId = HttpContext.CurrentUserId();
        return Ok(await _mediator.Send(command));
    }
}
