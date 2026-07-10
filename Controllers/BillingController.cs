using DammaniAPI.Features;
using DammaniAPI.Features.Billing;
using DammaniAPI.Middlewares.Authentication;
using DammaniAPI.Utilities;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace DammaniAPI.Controllers;

[ApiController]
[Route("billing")]
public class BillingController : ControllerBase
{
    private readonly IMediator _mediator;

    public BillingController(IMediator mediator) => _mediator = mediator;

    [Authorize(Roles.Staff)]
    [HttpGet("getUsage")]
    public async Task<IActionResult> GetUsage([FromQuery] GetUsage.Query query)
    {
        query.ShopId = HttpContext.CurrentShopId();
        return Ok(await _mediator.Send(query));
    }

    [Authorize(Roles.Staff)]
    [HttpGet("getPlans")]
    public async Task<IActionResult> GetPlans([FromQuery] GetPlans.Query query)
        => Ok(await _mediator.Send(query));

    [Authorize(Roles.Owner)]
    [HttpGet("getBillingOverview")]
    public async Task<IActionResult> GetBillingOverview([FromQuery] GetBillingOverview.Query query)
    {
        query.ShopId = HttpContext.CurrentShopId();
        return Ok(await _mediator.Send(query));
    }

    [Authorize(Roles.Owner)]
    [HttpGet("getPayments")]
    public async Task<IActionResult> GetPayments([FromQuery] GetPayments.Query query)
    {
        query.ShopId = HttpContext.CurrentShopId();
        return Ok(await _mediator.Send(query));
    }

    // Manual-activation upgrade request (DMN-1003). Gateway checkout drops in here later.
    [Authorize(Roles.Owner)]
    [HttpPost("requestUpgrade")]
    public async Task<IActionResult> RequestUpgrade([FromBody] RequestUpgrade.Command command)
    {
        command.ShopId = HttpContext.CurrentShopId();
        command.ActorUserId = HttpContext.CurrentUserId();
        return Ok(await _mediator.Send(command));
    }

    [Authorize(Roles.Owner)]
    [HttpPost("scheduleDowngrade")]
    public async Task<IActionResult> ScheduleDowngrade([FromBody] ScheduleDowngrade.Command command)
    {
        command.ShopId = HttpContext.CurrentShopId();
        command.ActorUserId = HttpContext.CurrentUserId();
        return Ok(await _mediator.Send(command));
    }

    [Authorize(Roles.Owner)]
    [HttpPost("cancelSubscription")]
    public async Task<IActionResult> CancelSubscription([FromBody] CancelSubscription.Command command)
    {
        command.ShopId = HttpContext.CurrentShopId();
        command.ActorUserId = HttpContext.CurrentUserId();
        return Ok(await _mediator.Send(command));
    }

    [Authorize(Roles.Owner)]
    [HttpPost("revertScheduledChange")]
    public async Task<IActionResult> RevertScheduledChange([FromBody] RevertScheduledChange.Command command)
    {
        command.ShopId = HttpContext.CurrentShopId();
        command.ActorUserId = HttpContext.CurrentUserId();
        return Ok(await _mediator.Send(command));
    }
}
