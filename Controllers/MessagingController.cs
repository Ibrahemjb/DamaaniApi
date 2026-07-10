using DammaniAPI.Features;
using DammaniAPI.Features.Messaging;
using DammaniAPI.Middlewares.Authentication;
using DammaniAPI.Utilities;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace DammaniAPI.Controllers;

[ApiController]
[Route("messaging")]
public class MessagingController : ControllerBase
{
    private readonly IMediator _mediator;

    public MessagingController(IMediator mediator) => _mediator = mediator;

    [Authorize(Roles.Staff)]
    [HttpGet("getTemplates")]
    public async Task<IActionResult> GetTemplates([FromQuery] GetMessageTemplates.Query query)
    {
        query.ShopId = HttpContext.CurrentShopId();
        return Ok(await _mediator.Send(query));
    }
}
