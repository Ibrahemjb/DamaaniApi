using DammaniAPI.Features;
using DammaniAPI.Features.Onboarding;
using DammaniAPI.Middlewares.Authentication;
using DammaniAPI.Utilities;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace DammaniAPI.Controllers;

[ApiController]
[Route("onboarding")]
public class OnboardingController : ControllerBase
{
    private readonly IMediator _mediator;

    public OnboardingController(IMediator mediator) => _mediator = mediator;

    [Authorize(Roles.Owner)]
    [HttpGet("getState")]
    public async Task<IActionResult> GetState()
        => Ok(await _mediator.Send(new GetOnboardingState.Query { ShopId = HttpContext.CurrentShopId() }));

    [Authorize(Roles.Owner)]
    [HttpPost("saveShopIdentity")]
    public async Task<IActionResult> SaveShopIdentity([FromBody] SaveShopIdentity.Command command)
    {
        command.ShopId = HttpContext.CurrentShopId();
        return Ok(await _mediator.Send(command));
    }

    [Authorize(Roles.Owner)]
    [HttpPost("selectCategories")]
    public async Task<IActionResult> SelectCategories([FromBody] SelectCategories.Command command)
    {
        command.ShopId = HttpContext.CurrentShopId();
        return Ok(await _mediator.Send(command));
    }

    [Authorize(Roles.Owner)]
    [HttpPost("saveDefaultTerms")]
    public async Task<IActionResult> SaveDefaultTerms([FromBody] SaveDefaultTerms.Command command)
    {
        command.ShopId = HttpContext.CurrentShopId();
        return Ok(await _mediator.Send(command));
    }

    [Authorize(Roles.Owner)]
    [HttpPost("complete")]
    public async Task<IActionResult> Complete()
        => Ok(await _mediator.Send(new CompleteOnboarding.Command
        {
            ShopId = HttpContext.CurrentShopId(),
            ActorUserId = HttpContext.CurrentUserId()
        }));

    [Authorize(Roles.Owner)]
    [HttpPost("uploadLogo")]
    [RequestSizeLimit(3_000_000)]
    public async Task<IActionResult> UploadLogo(IFormFile? file)
    {
        if (file == null)
            return Ok(new UploadLogo.Result { Success = false, ErrorCode = ErrorCodes.InvalidFiles });

        using var buffer = new MemoryStream();
        await file.CopyToAsync(buffer);
        return Ok(await _mediator.Send(new UploadLogo.Command
        {
            ShopId = HttpContext.CurrentShopId(),
            File = new UploadLogo.FilePayload
            {
                FileName = file.FileName,
                ContentType = file.ContentType,
                Content = buffer.ToArray()
            }
        }));
    }
}
