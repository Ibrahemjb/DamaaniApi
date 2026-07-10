using DammaniAPI.Features;
using DammaniAPI.Features.Customers;
using DammaniAPI.Middlewares.Authentication;
using DammaniAPI.Utilities;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace DammaniAPI.Controllers;

[ApiController]
[Route("customers")]
public class CustomersController : ControllerBase
{
    private readonly IMediator _mediator;

    public CustomersController(IMediator mediator) => _mediator = mediator;

    [Authorize(Roles.Staff)]
    [HttpGet("searchCustomers")]
    public async Task<IActionResult> SearchCustomers([FromQuery] SearchCustomers.Query query)
    {
        query.ShopId = HttpContext.CurrentShopId();
        return Ok(await _mediator.Send(query));
    }

    [Authorize(Roles.Staff)]
    [HttpGet("getCustomerHistory")]
    public async Task<IActionResult> GetCustomerHistory([FromQuery] GetCustomerHistory.Query query)
    {
        query.ShopId = HttpContext.CurrentShopId();
        return Ok(await _mediator.Send(query));
    }
}
