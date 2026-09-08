using Garage.Application.Households;
using Garage.Domain.Common;
using Garage.Web.Api.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Garage.Web.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/households")]
public class HouseholdsController(HouseholdService households) : ControllerBase
{
    [HttpGet("overview")]
    public async Task<ActionResult<HouseholdOverview>> GetOverviewAsync(CancellationToken cancellationToken)
    {
        try
        {
            var overview = await households.GetOverviewAsync(cancellationToken);
            return Ok(overview);
        }
        catch (DomainException ex)
        {
            return BadRequest(new ApiErrorResponse(ex.Message));
        }
    }

    [HttpPost("invite")]
    public async Task<ActionResult<CreatedInvitation>> InviteAsync(CancellationToken cancellationToken)
    {
        try
        {
            var invitation = await households.InviteAsync(cancellationToken);
            return Ok(invitation);
        }
        catch (DomainException ex)
        {
            return BadRequest(new ApiErrorResponse(ex.Message));
        }
    }

    [HttpPost("invitations/{invitationId:guid}/revoke")]
    public async Task<IActionResult> RevokeAsync(Guid invitationId, CancellationToken cancellationToken)
    {
        try
        {
            await households.RevokeAsync(invitationId, cancellationToken);
            return NoContent();
        }
        catch (DomainException ex)
        {
            return BadRequest(new ApiErrorResponse(ex.Message));
        }
    }

    [HttpPost("preview")]
    public async Task<ActionResult<InvitationPreview>> PreviewAsync([FromBody] InvitationCodeRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var preview = await households.PreviewAsync(request.Code, cancellationToken);
            return Ok(preview);
        }
        catch (DomainException ex)
        {
            return BadRequest(new ApiErrorResponse(ex.Message));
        }
    }

    [HttpPost("accept")]
    public async Task<IActionResult> AcceptAsync([FromBody] InvitationCodeRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var householdId = await households.AcceptAsync(request.Code, cancellationToken);
            return Ok(new { HouseholdId = householdId });
        }
        catch (DomainException ex)
        {
            return BadRequest(new ApiErrorResponse(ex.Message));
        }
    }

    [HttpPost("leave")]
    public async Task<IActionResult> LeaveAsync(CancellationToken cancellationToken)
    {
        try
        {
            await households.LeaveAsync(cancellationToken);
            return NoContent();
        }
        catch (DomainException ex)
        {
            return BadRequest(new ApiErrorResponse(ex.Message));
        }
    }
}
