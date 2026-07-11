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
    [AuthorizeAdmin(AdminRoles.Super, AdminRoles.Support)]
    public async Task<IActionResult> SuspendShop([FromBody] SuspendShop.Command command)
    {
        command.ActorUserId = HttpContext.CurrentUserId();
        return Ok(await _mediator.Send(command));
    }

    [HttpPost("reactivateShop")]
    [AuthorizeAdmin(AdminRoles.Super, AdminRoles.Support)]
    public async Task<IActionResult> ReactivateShop([FromBody] ReactivateShop.Command command)
    {
        command.ActorUserId = HttpContext.CurrentUserId();
        return Ok(await _mediator.Send(command));
    }

    [HttpGet("getPlanOverview")]
    public async Task<IActionResult> GetPlanOverview()
        => Ok(await _mediator.Send(new GetPlanOverview.Query()));

    [HttpPost("confirmUpgrade")]
    [AuthorizeAdmin(AdminRoles.Super, AdminRoles.Billing)]
    public async Task<IActionResult> ConfirmUpgrade([FromBody] ConfirmUpgrade.Command command)
    {
        command.ActorUserId = HttpContext.CurrentUserId();
        return Ok(await _mediator.Send(command));
    }

    [HttpPost("rejectUpgrade")]
    [AuthorizeAdmin(AdminRoles.Super, AdminRoles.Billing)]
    public async Task<IActionResult> RejectUpgrade([FromBody] RejectUpgrade.Command command)
    {
        command.ActorUserId = HttpContext.CurrentUserId();
        return Ok(await _mediator.Send(command));
    }

    [HttpPost("changeShopPlan")]
    [AuthorizeAdmin(AdminRoles.Super, AdminRoles.Billing)]
    public async Task<IActionResult> ChangeShopPlan([FromBody] ChangeShopPlan.Command command)
    {
        command.ActorUserId = HttpContext.CurrentUserId();
        return Ok(await _mediator.Send(command));
    }

    [HttpPost("grantExtension")]
    [AuthorizeAdmin(AdminRoles.Super, AdminRoles.Billing)]
    public async Task<IActionResult> GrantExtension([FromBody] GrantExtension.Command command)
    {
        command.ActorUserId = HttpContext.CurrentUserId();
        return Ok(await _mediator.Send(command));
    }

    [HttpGet("getDefaultTemplates")]
    public async Task<IActionResult> GetDefaultTemplates()
        => Ok(await _mediator.Send(new GetDefaultTemplates.Query()));

    [HttpPost("updateDefaultTemplate")]
    [AuthorizeAdmin(AdminRoles.Super, AdminRoles.Content)]
    public async Task<IActionResult> UpdateDefaultTemplate([FromBody] UpdateDefaultTemplate.Command command)
    {
        command.ActorUserId = HttpContext.CurrentUserId();
        return Ok(await _mediator.Send(command));
    }

    [HttpGet("getPlatformMessages")]
    public async Task<IActionResult> GetPlatformMessages()
        => Ok(await _mediator.Send(new GetPlatformMessages.Query()));

    [HttpPost("updatePlatformMessage")]
    [AuthorizeAdmin(AdminRoles.Super, AdminRoles.Content)]
    public async Task<IActionResult> UpdatePlatformMessage([FromBody] UpdatePlatformMessage.Command command)
    {
        command.ActorUserId = HttpContext.CurrentUserId();
        return Ok(await _mediator.Send(command));
    }

    [HttpPost("resetPlatformMessage")]
    [AuthorizeAdmin(AdminRoles.Super, AdminRoles.Content)]
    public async Task<IActionResult> ResetPlatformMessage([FromBody] ResetPlatformMessage.Command command)
    {
        command.ActorUserId = HttpContext.CurrentUserId();
        return Ok(await _mediator.Send(command));
    }

    [HttpGet("getContentBlocks")]
    public async Task<IActionResult> GetContentBlocks()
        => Ok(await _mediator.Send(new GetContentBlocks.Query()));

    [HttpPost("updateContentBlock")]
    [AuthorizeAdmin(AdminRoles.Super, AdminRoles.Content)]
    public async Task<IActionResult> UpdateContentBlock([FromBody] UpdateContentBlock.Command command)
    {
        command.ActorUserId = HttpContext.CurrentUserId();
        return Ok(await _mediator.Send(command));
    }

    [HttpGet("search")]
    public async Task<IActionResult> Search([FromQuery] SearchAdmin.Query query)
        => Ok(await _mediator.Send(query));

    [HttpGet("getRevenueSummary")]
    public async Task<IActionResult> GetRevenueSummary()
        => Ok(await _mediator.Send(new GetRevenueSummary.Query()));

    [HttpGet("getPaymentLedger")]
    public async Task<IActionResult> GetPaymentLedger([FromQuery] GetPaymentLedger.Query query)
        => Ok(await _mediator.Send(query));

    [HttpGet("getContactMessages")]
    public async Task<IActionResult> GetContactMessages([FromQuery] GetContactMessages.Query query)
        => Ok(await _mediator.Send(query));

    [HttpPost("updateContactMessage")]
    [AuthorizeAdmin(AdminRoles.Super, AdminRoles.Support)]
    public async Task<IActionResult> UpdateContactMessage([FromBody] UpdateContactMessage.Command command)
    {
        command.ActorUserId = HttpContext.CurrentUserId();
        return Ok(await _mediator.Send(command));
    }

    [HttpGet("getActivity")]
    public async Task<IActionResult> GetActivity([FromQuery] GetActivity.Query query)
        => Ok(await _mediator.Send(query));

    [HttpGet("getAlerts")]
    public async Task<IActionResult> GetAlerts()
        => Ok(await _mediator.Send(new GetAdminAlerts.Query()));

    [HttpGet("getRiskSignals")]
    public async Task<IActionResult> GetRiskSignals()
        => Ok(await _mediator.Send(new GetRiskSignals.Query()));

    [HttpGet("getSystemHealth")]
    public async Task<IActionResult> GetSystemHealth()
        => Ok(await _mediator.Send(new GetSystemHealth.Query()));

    [HttpGet("getAdminUsers")]
    [AuthorizeAdmin(AdminRoles.Super)]
    public async Task<IActionResult> GetAdminUsers()
        => Ok(await _mediator.Send(new GetAdminUsers.Query()));

    [HttpPost("setAdminRole")]
    [AuthorizeAdmin(AdminRoles.Super)]
    public async Task<IActionResult> SetAdminRole([FromBody] SetAdminRole.Command command)
    {
        command.ActorUserId = HttpContext.CurrentUserId();
        return Ok(await _mediator.Send(command));
    }
}
