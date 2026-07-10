using DammaniAPI.Features;
using DammaniAPI.Features.Dashboard;
using DammaniAPI.Middlewares.Authentication;
using DammaniAPI.Utilities;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace DammaniAPI.Controllers;

[ApiController]
[Route("dashboard")]
public class DashboardController : ControllerBase
{
    private readonly IMediator _mediator;

    public DashboardController(IMediator mediator) => _mediator = mediator;

    [Authorize(Roles.Staff)]
    [HttpGet("getSummary")]
    public async Task<IActionResult> GetSummary([FromQuery] GetSummary.Query query)
    {
        query.ShopId = HttpContext.CurrentShopId();
        return Ok(await _mediator.Send(query));
    }
}
