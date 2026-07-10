using DammaniAPI.Features.Admin;
using DammaniAPI.Middlewares.Authentication;
using DammaniAPI.Utilities;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace DammaniAPI.Controllers;

[ApiController]
[Route("admin")]
[AuthorizeAdmin]
public class AdminController : ControllerBase
{
    private readonly IMediator _mediator;

    public AdminController(IMediator mediator) => _mediator = mediator;

    [HttpGet("getDashboard")]
    public async Task<IActionResult> GetDashboard()
        => Ok(await _mediator.Send(new GetAdminDashboard.Query()));

    [HttpGet("getShops")]
    public async Task<IActionResult> GetShops([FromQuery] GetShops.Query query)
        => Ok(await _mediator.Send(query));

    [HttpGet("getShop")]
    public async Task<IActionResult> GetShop([FromQuery] GetShop.Query query)
        => Ok(await _mediator.Send(query));

    [HttpPost("suspendShop")]
    public async Task<IActionResult> SuspendShop([FromBody] SuspendShop.Command command)
    {
        command.ActorUserId = HttpContext.CurrentUserId();
        return Ok(await _mediator.Send(command));
    }

    [HttpPost("reactivateShop")]
    public async Task<IActionResult> ReactivateShop([FromBody] ReactivateShop.Command command)
    {
        command.ActorUserId = HttpContext.CurrentUserId();
        return Ok(await _mediator.Send(command));
    }

    [HttpGet("getPlanOverview")]
    public async Task<IActionResult> GetPlanOverview()
        => Ok(await _mediator.Send(new GetPlanOverview.Query()));

    [HttpPost("confirmUpgrade")]
    public async Task<IActionResult> ConfirmUpgrade([FromBody] ConfirmUpgrade.Command command)
    {
        command.ActorUserId = HttpContext.CurrentUserId();
        return Ok(await _mediator.Send(command));
    }

    [HttpPost("rejectUpgrade")]
    public async Task<IActionResult> RejectUpgrade([FromBody] RejectUpgrade.Command command)
    {
        command.ActorUserId = HttpContext.CurrentUserId();
        return Ok(await _mediator.Send(command));
    }

    [HttpPost("changeShopPlan")]
    public async Task<IActionResult> ChangeShopPlan([FromBody] ChangeShopPlan.Command command)
    {
        command.ActorUserId = HttpContext.CurrentUserId();
        return Ok(await _mediator.Send(command));
    }

    [HttpPost("grantExtension")]
    public async Task<IActionResult> GrantExtension([FromBody] GrantExtension.Command command)
    {
        command.ActorUserId = HttpContext.CurrentUserId();
        return Ok(await _mediator.Send(command));
    }

    [HttpGet("getDefaultTemplates")]
    public async Task<IActionResult> GetDefaultTemplates()
        => Ok(await _mediator.Send(new GetDefaultTemplates.Query()));

    [HttpPost("updateDefaultTemplate")]
    public async Task<IActionResult> UpdateDefaultTemplate([FromBody] UpdateDefaultTemplate.Command command)
    {
        command.ActorUserId = HttpContext.CurrentUserId();
        return Ok(await _mediator.Send(command));
    }

    [HttpGet("getPlatformMessages")]
    public async Task<IActionResult> GetPlatformMessages()
        => Ok(await _mediator.Send(new GetPlatformMessages.Query()));

    [HttpPost("updatePlatformMessage")]
    public async Task<IActionResult> UpdatePlatformMessage([FromBody] UpdatePlatformMessage.Command command)
    {
        command.ActorUserId = HttpContext.CurrentUserId();
        return Ok(await _mediator.Send(command));
    }

    [HttpPost("resetPlatformMessage")]
    public async Task<IActionResult> ResetPlatformMessage([FromBody] ResetPlatformMessage.Command command)
    {
        command.ActorUserId = HttpContext.CurrentUserId();
        return Ok(await _mediator.Send(command));
    }

    [HttpGet("getContentBlocks")]
    public async Task<IActionResult> GetContentBlocks()
        => Ok(await _mediator.Send(new GetContentBlocks.Query()));

    [HttpPost("updateContentBlock")]
    public async Task<IActionResult> UpdateContentBlock([FromBody] UpdateContentBlock.Command command)
    {
        command.ActorUserId = HttpContext.CurrentUserId();
        return Ok(await _mediator.Send(command));
    }
}
