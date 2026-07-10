using DammaniAPI.Features.Public;
using DammaniAPI.Features.Staff;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace DammaniAPI.Controllers;

// Customer-facing endpoints. No [Authorize]: /public/* is whitelisted in
// AuthenticationMiddleware (DMN-202). No response caching on the warranty
// lookup — a cancellation must reflect on the very next scan (DMN-501).
[ApiController]
[Route("public")]
public class PublicController : ControllerBase
{
    private readonly IMediator _mediator;

    public PublicController(IMediator mediator) => _mediator = mediator;

    [HttpGet("warranty/{slug}")]
    public async Task<IActionResult> GetWarranty(string slug)
        => Ok(await _mediator.Send(new GetPublicWarranty.Query { Slug = slug }));

    // Multipart form: scalar fields bind to the command (auto-validated at
    // binding time); files bind separately and are mapped to HTTP-free
    // payloads here so the handler owns the authoritative file checks.
    // ClientIp and Files are always overwritten — a form field with those
    // names cannot inject values.
    [HttpPost("serviceRequests/submit")]
    [RequestSizeLimit(20_000_000)]
    public async Task<IActionResult> SubmitServiceRequest(
        [FromForm] SubmitServiceRequest.Command command, [FromForm] List<IFormFile>? files)
    {
        command.ClientIp = HttpContext.Connection.RemoteIpAddress?.ToString();
        command.Files = new List<SubmitServiceRequest.FilePayload>();
        foreach (var file in files ?? [])
        {
            using var buffer = new MemoryStream();
            await file.CopyToAsync(buffer);
            command.Files.Add(new SubmitServiceRequest.FilePayload
            {
                FileName = file.FileName,
                ContentType = file.ContentType,
                Content = buffer.ToArray()
            });
        }
        return Ok(await _mediator.Send(command));
    }

    [HttpGet("content")]
    public async Task<IActionResult> GetContent([FromQuery] string[] keys)
        => Ok(await _mediator.Send(new GetContent.Query { Keys = keys }));

    [HttpGet("invite/{token}")]
    public async Task<IActionResult> GetInvite(string token)
        => Ok(await _mediator.Send(new GetInvite.Query { Token = token }));

    [HttpPost("invite/accept")]
    public async Task<IActionResult> AcceptInvite([FromBody] AcceptInvite.Command command)
        => Ok(await _mediator.Send(command));

    [HttpPost("contact/submit")]
    public async Task<IActionResult> SubmitContact([FromBody] SubmitContact.Command command)
    {
        command.ClientIp = HttpContext.Connection.RemoteIpAddress?.ToString();
        command.ShopId = HttpContext.Items["ShopId"] as string;
        return Ok(await _mediator.Send(command));
    }
}
