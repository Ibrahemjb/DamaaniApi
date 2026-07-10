using DammaniAPI.Features;
using DammaniAPI.Features.ServiceRequests;
using DammaniAPI.Middlewares.Authentication;
using DammaniAPI.Utilities;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace DammaniAPI.Controllers;

[ApiController]
[Route("serviceRequests")]
public class ServiceRequestsController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IConfiguration _configuration;

    public ServiceRequestsController(IMediator mediator, IConfiguration configuration)
    {
        _mediator = mediator;
        _configuration = configuration;
    }

    [Authorize(Roles.Staff)]
    [HttpGet("getServiceRequests")]
    public async Task<IActionResult> GetServiceRequests([FromQuery] GetServiceRequests.Query query)
    {
        query.ShopId = HttpContext.CurrentShopId();
        return Ok(await _mediator.Send(query));
    }

    [Authorize(Roles.Staff)]
    [HttpGet("getServiceRequest")]
    public async Task<IActionResult> GetServiceRequest([FromQuery] GetServiceRequest.Query query)
    {
        query.ShopId = HttpContext.CurrentShopId();
        return Ok(await _mediator.Send(query));
    }

    [Authorize(Roles.Staff)]
    [HttpPost("changeStatus")]
    public async Task<IActionResult> ChangeStatus([FromBody] ChangeStatus.Command command)
    {
        command.ShopId = HttpContext.CurrentShopId();
        command.ActorUserId = HttpContext.CurrentUserId();
        return Ok(await _mediator.Send(command));
    }

    [Authorize(Roles.Staff)]
    [HttpPost("addNote")]
    public async Task<IActionResult> AddNote([FromBody] AddNote.Command command)
    {
        command.ShopId = HttpContext.CurrentShopId();
        command.ActorUserId = HttpContext.CurrentUserId();
        return Ok(await _mediator.Send(command));
    }

    [Authorize(Roles.Staff)]
    [HttpPost("assign")]
    public async Task<IActionResult> Assign([FromBody] Assign.Command command)
    {
        command.ShopId = HttpContext.CurrentShopId();
        command.ActorUserId = HttpContext.CurrentUserId();
        return Ok(await _mediator.Send(command));
    }

    [Authorize(Roles.Staff)]
    [HttpPost("closeRequest")]
    public async Task<IActionResult> CloseRequest([FromBody] CloseRequest.Command command)
    {
        command.ShopId = HttpContext.CurrentShopId();
        command.ActorUserId = HttpContext.CurrentUserId();
        return Ok(await _mediator.Send(command));
    }

    [Authorize(Roles.Staff)]
    [HttpPost("createInternal")]
    public async Task<IActionResult> CreateInternal([FromBody] CreateInternal.Command command)
    {
        command.ShopId = HttpContext.CurrentShopId();
        command.ActorUserId = HttpContext.CurrentUserId();
        return Ok(await _mediator.Send(command));
    }

    // Authenticated attachment stream — never expose uploads root publicly (DMN-602).
    [Authorize(Roles.Staff)]
    [HttpGet("getAttachment")]
    public async Task<IActionResult> GetAttachment([FromQuery] GetAttachment.Query query)
    {
        query.ShopId = HttpContext.CurrentShopId();
        var result = await _mediator.Send(query);
        if (!result.Success)
            return Ok(result);

        var uploadsRoot = _configuration["UPLOADS_ROOT"] ?? Path.Combine(AppContext.BaseDirectory, "uploads");
        var fullPath = Path.Combine(uploadsRoot, result.FilePath!.Replace('/', Path.DirectorySeparatorChar));
        if (!System.IO.File.Exists(fullPath))
            return Ok(new { success = false, errorCode = ErrorCodes.NotFound });

        var downloadName = result.ContentType!.StartsWith("image/", StringComparison.OrdinalIgnoreCase)
            ? null
            : result.OriginalName;
        return PhysicalFile(fullPath, result.ContentType!, downloadName);
    }
}
