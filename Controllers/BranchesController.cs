using DammaniAPI.Features;
using DammaniAPI.Features.Branches;
using DammaniAPI.Middlewares.Authentication;
using DammaniAPI.Utilities;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace DammaniAPI.Controllers;

[ApiController]
[Route("branches")]
public class BranchesController : ControllerBase
{
    private readonly IMediator _mediator;

    public BranchesController(IMediator mediator) => _mediator = mediator;

    [Authorize(Roles.Staff)]
    [HttpGet("getBranches")]
    public async Task<IActionResult> GetBranches([FromQuery] GetBranches.Query query)
    {
        query.ShopId = HttpContext.CurrentShopId();
        return Ok(await _mediator.Send(query));
    }

    [Authorize(Roles.Owner)]
    [HttpPost("createBranch")]
    public async Task<IActionResult> CreateBranch([FromBody] CreateBranch.Command command)
    {
        command.ShopId = HttpContext.CurrentShopId();
        command.ActorUserId = HttpContext.CurrentUserId();
        return Ok(await _mediator.Send(command));
    }

    [Authorize(Roles.Owner)]
    [HttpPost("updateBranch")]
    public async Task<IActionResult> UpdateBranch([FromBody] UpdateBranch.Command command)
    {
        command.ShopId = HttpContext.CurrentShopId();
        command.ActorUserId = HttpContext.CurrentUserId();
        return Ok(await _mediator.Send(command));
    }

    [Authorize(Roles.Owner)]
    [HttpPost("setBranchStatus")]
    public async Task<IActionResult> SetBranchStatus([FromBody] SetBranchStatus.Command command)
    {
        command.ShopId = HttpContext.CurrentShopId();
        command.ActorUserId = HttpContext.CurrentUserId();
        return Ok(await _mediator.Send(command));
    }
}
