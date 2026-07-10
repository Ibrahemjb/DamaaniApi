using DammaniAPI.Features;
using DammaniAPI.Features.Reports;
using DammaniAPI.Middlewares.Authentication;
using DammaniAPI.Utilities;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace DammaniAPI.Controllers;

[ApiController]
[Route("reports")]
public class ReportsController : ControllerBase
{
    private readonly IMediator _mediator;

    public ReportsController(IMediator mediator) => _mediator = mediator;

    [Authorize(Roles.Staff)]
    [HttpGet("getReports")]
    public async Task<IActionResult> GetReports([FromQuery] GetReports.Query query)
    {
        query.ShopId = HttpContext.CurrentShopId();
        return Ok(await _mediator.Send(query));
    }
}
