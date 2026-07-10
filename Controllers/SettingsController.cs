using DammaniAPI.Features;
using DammaniAPI.Features.Messaging;
using DammaniAPI.Features.Settings;
using DammaniAPI.Middlewares.Authentication;
using DammaniAPI.Utilities;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace DammaniAPI.Controllers;

[ApiController]
[Route("settings")]
public class SettingsController : ControllerBase
{
    private readonly IMediator _mediator;

    public SettingsController(IMediator mediator) => _mediator = mediator;

    [Authorize(Roles.Owner)]
    [HttpGet("getShopSettings")]
    public async Task<IActionResult> GetShopSettings()
        => Ok(await _mediator.Send(new GetShopSettings.Query { ShopId = HttpContext.CurrentShopId() }));

    [Authorize(Roles.Owner)]
    [HttpPost("updateShopProfile")]
    public async Task<IActionResult> UpdateShopProfile([FromBody] UpdateShopProfile.Command command)
    {
        command.ShopId = HttpContext.CurrentShopId();
        command.ActorUserId = HttpContext.CurrentUserId();
        return Ok(await _mediator.Send(command));
    }

    [Authorize(Roles.Owner)]
    [HttpPost("updatePublicPageSettings")]
    public async Task<IActionResult> UpdatePublicPageSettings([FromBody] UpdatePublicPageSettings.Command command)
    {
        command.ShopId = HttpContext.CurrentShopId();
        command.ActorUserId = HttpContext.CurrentUserId();
        return Ok(await _mediator.Send(command));
    }

    [Authorize(Roles.Owner)]
    [HttpPost("updateWarrantySettings")]
    public async Task<IActionResult> UpdateWarrantySettings([FromBody] UpdateWarrantySettings.Command command)
    {
        command.ShopId = HttpContext.CurrentShopId();
        command.ActorUserId = HttpContext.CurrentUserId();
        return Ok(await _mediator.Send(command));
    }

    [Authorize(Roles.Owner)]
    [HttpPost("updateNotificationSettings")]
    public async Task<IActionResult> UpdateNotificationSettings([FromBody] UpdateNotificationSettings.Command command)
    {
        command.ShopId = HttpContext.CurrentShopId();
        command.ActorUserId = HttpContext.CurrentUserId();
        return Ok(await _mediator.Send(command));
    }

    [Authorize(Roles.Owner)]
    [HttpPost("updateMessageTemplate")]
    public async Task<IActionResult> UpdateMessageTemplate([FromBody] UpdateMessageTemplate.Command command)
    {
        command.ShopId = HttpContext.CurrentShopId();
        command.ActorUserId = HttpContext.CurrentUserId();
        return Ok(await _mediator.Send(command));
    }

    [Authorize(Roles.Owner)]
    [HttpPost("resetMessageTemplate")]
    public async Task<IActionResult> ResetMessageTemplate([FromBody] ResetMessageTemplate.Command command)
    {
        command.ShopId = HttpContext.CurrentShopId();
        command.ActorUserId = HttpContext.CurrentUserId();
        return Ok(await _mediator.Send(command));
    }
}
