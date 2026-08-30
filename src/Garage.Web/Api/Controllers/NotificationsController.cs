using Garage.Application.Notifications;
using Garage.Domain.Common;
using Garage.Web.Api.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Garage.Web.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/notifications")]
public class NotificationsController(PushSubscriptionService push) : ControllerBase
{
    [HttpGet("status")]
    public async Task<ActionResult<NotificationStatusResponse>> GetStatusAsync(CancellationToken cancellationToken)
    {
        var count = await push.CountAsync(cancellationToken);
        return Ok(new NotificationStatusResponse(push.IsConfigured, push.PublicKey, count));
    }

    [HttpPost("subscribe")]
    public async Task<IActionResult> SubscribeAsync([FromBody] PushSubscriptionRequest request, CancellationToken cancellationToken)
    {
        try
        {
            await push.SubscribeAsync(request, cancellationToken);
            return NoContent();
        }
        catch (DomainException ex)
        {
            return BadRequest(new ApiErrorResponse(ex.Message));
        }
    }

    [HttpPost("unsubscribe")]
    public async Task<IActionResult> UnsubscribeAsync([FromBody] UnsubscribeRequest request, CancellationToken cancellationToken)
    {
        await push.UnsubscribeAsync(request.Endpoint, cancellationToken);
        return NoContent();
    }
}
