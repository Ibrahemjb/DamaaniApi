using DammaniAPI.Features;
using DammaniAPI.Features.Warranties;
using DammaniAPI.Middlewares.Authentication;
using DammaniAPI.Utilities;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace DammaniAPI.Controllers;

[ApiController]
[Route("warranties")]
public class WarrantiesController : ControllerBase
{
    private readonly IMediator _mediator;

    public WarrantiesController(IMediator mediator) => _mediator = mediator;

    [Authorize(Roles.Staff)]
    [HttpGet("getCreateWarrantyContext")]
    public async Task<IActionResult> GetCreateWarrantyContext([FromQuery] GetCreateWarrantyContext.Query query)
    {
        query.ShopId = HttpContext.CurrentShopId();
        return Ok(await _mediator.Send(query));
    }

    [Authorize(Roles.Staff)]
    [HttpGet("checkSerialDuplicate")]
    public async Task<IActionResult> CheckSerialDuplicate([FromQuery] CheckSerialDuplicate.Query query)
    {
        query.ShopId = HttpContext.CurrentShopId();
        return Ok(await _mediator.Send(query));
    }

    [Authorize(Roles.Staff)]
    [HttpPost("createWarranty")]
    public async Task<IActionResult> CreateWarranty([FromBody] CreateWarranty.Command command)
    {
        command.ShopId = HttpContext.CurrentShopId();
        command.ActorUserId = HttpContext.CurrentUserId();
        return Ok(await _mediator.Send(command));
    }

    [Authorize(Roles.Staff)]
    [HttpGet("getWarranties")]
    public async Task<IActionResult> GetWarranties([FromQuery] GetWarranties.Query query)
    {
        query.ShopId = HttpContext.CurrentShopId();
        return Ok(await _mediator.Send(query));
    }

    [Authorize(Roles.Staff)]
    [HttpGet("getWarranty")]
    public async Task<IActionResult> GetWarranty([FromQuery] GetWarranty.Query query)
    {
        query.ShopId = HttpContext.CurrentShopId();
        return Ok(await _mediator.Send(query));
    }

    [Authorize(Roles.Staff)]
    [HttpPost("updateWarranty")]
    public async Task<IActionResult> UpdateWarranty([FromBody] UpdateWarranty.Command command)
    {
        command.ShopId = HttpContext.CurrentShopId();
        command.ActorUserId = HttpContext.CurrentUserId();
        return Ok(await _mediator.Send(command));
    }

    // Owner-only: staff may edit but never cancel (BP §10.13, DMN-407).
    [Authorize(Roles.Owner)]
    [HttpPost("cancelWarranty")]
    public async Task<IActionResult> CancelWarranty([FromBody] CancelWarranty.Command command)
    {
        command.ShopId = HttpContext.CurrentShopId();
        command.ActorUserId = HttpContext.CurrentUserId();
        return Ok(await _mediator.Send(command));
    }

    // The one controller action that returns a file instead of Ok(result):
    // CSV download needs Content-Disposition (DMN-409 documented exception).
    [Authorize(Roles.Staff)]
    [HttpPost("logShare")]
    public async Task<IActionResult> LogShare([FromBody] LogShare.Command command)
    {
        command.ShopId = HttpContext.CurrentShopId();
        command.ActorUserId = HttpContext.CurrentUserId();
        return Ok(await _mediator.Send(command));
    }

    [Authorize(Roles.Staff)]
    [HttpGet("exportWarranties")]
    public async Task<IActionResult> ExportWarranties([FromQuery] ExportWarranties.Query query)
    {
        query.ShopId = HttpContext.CurrentShopId();
        query.ActorUserId = HttpContext.CurrentUserId();
        var result = await _mediator.Send(query);
        if (!result.Success)
            return Ok(result);
        return File(result.FileBytes!, "text/csv; charset=utf-8", result.FileName);
    }
}
