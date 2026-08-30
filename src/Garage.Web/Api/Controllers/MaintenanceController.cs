using Garage.Application.Abstractions;
using Garage.Application.Maintenance;
using Garage.Domain.Common;
using Garage.Web.Api.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Garage.Web.Api.Controllers;

[ApiController]
[Authorize]
[Route("api")]
public class MaintenanceController(MaintenanceService maintenance, IFileStore files) : ControllerBase
{
    [HttpGet("vehicles/{vehicleId:guid}/maintenance/upcoming")]
    public async Task<ActionResult<IReadOnlyList<ReminderCard>>> ListUpcomingAsync(Guid vehicleId, CancellationToken cancellationToken)
    {
        try
        {
            var cards = await maintenance.ListUpcomingAsync(vehicleId, cancellationToken);
            return Ok(cards);
        }
        catch (DomainException ex)
        {
            return BadRequest(new ApiErrorResponse(ex.Message));
        }
    }

    [HttpGet("vehicles/{vehicleId:guid}/maintenance/reminders")]
    public async Task<ActionResult<IReadOnlyList<ReminderCard>>> ListAllAsync(Guid vehicleId, CancellationToken cancellationToken)
    {
        try
        {
            var cards = await maintenance.ListAllAsync(vehicleId, cancellationToken);
            return Ok(cards);
        }
        catch (DomainException ex)
        {
            return BadRequest(new ApiErrorResponse(ex.Message));
        }
    }

    [HttpPost("vehicles/{vehicleId:guid}/maintenance/reminders/preview")]
    public async Task<ActionResult<ReminderPreview>> PreviewAsync(
        Guid vehicleId,
        [FromBody] ReminderRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var preview = await maintenance.PreviewAsync(vehicleId, request, cancellationToken);
            return Ok(preview);
        }
        catch (DomainException ex)
        {
            return BadRequest(new ApiErrorResponse(ex.Message));
        }
    }

    [HttpPost("vehicles/{vehicleId:guid}/maintenance/reminders")]
    public async Task<ActionResult<ReminderCard>> SaveAsync(
        Guid vehicleId,
        [FromBody] ReminderRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var reminder = await maintenance.SaveAsync(vehicleId, request, cancellationToken);
            var cards = await maintenance.ListAllAsync(vehicleId, cancellationToken);
            var card = cards.FirstOrDefault(c => c.Id == reminder.Id);
            return card is null ? Ok() : Ok(card);
        }
        catch (DomainException ex)
        {
            return BadRequest(new ApiErrorResponse(ex.Message));
        }
    }

    [HttpPost("maintenance/reminders/{reminderId:guid}/snooze")]
    public async Task<IActionResult> SnoozeAsync(Guid reminderId, [FromBody] SnoozeRequest request, CancellationToken cancellationToken)
    {
        try
        {
            await maintenance.SnoozeAsync(reminderId, request, cancellationToken);
            return NoContent();
        }
        catch (DomainException ex)
        {
            return BadRequest(new ApiErrorResponse(ex.Message));
        }
    }

    [HttpPost("maintenance/reminders/{reminderId:guid}/dismiss")]
    public async Task<IActionResult> DismissAsync(Guid reminderId, CancellationToken cancellationToken)
    {
        try
        {
            await maintenance.DismissAsync(reminderId, cancellationToken);
            return NoContent();
        }
        catch (DomainException ex)
        {
            return BadRequest(new ApiErrorResponse(ex.Message));
        }
    }

    [HttpPost("maintenance/reminders/{reminderId:guid}/reinstate")]
    public async Task<IActionResult> ReinstateAsync(Guid reminderId, CancellationToken cancellationToken)
    {
        try
        {
            await maintenance.ReinstateAsync(reminderId, cancellationToken);
            return NoContent();
        }
        catch (DomainException ex)
        {
            return BadRequest(new ApiErrorResponse(ex.Message));
        }
    }

    [HttpPost("maintenance/reminders/{reminderId:guid}/notifications")]
    public async Task<IActionResult> SetNotificationsAsync(
        Guid reminderId,
        [FromBody] SetReminderNotificationsRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            await maintenance.SetNotificationsAsync(reminderId, request.Enabled, cancellationToken);
            return NoContent();
        }
        catch (DomainException ex)
        {
            return BadRequest(new ApiErrorResponse(ex.Message));
        }
    }

    [HttpGet("vehicles/{vehicleId:guid}/maintenance/history")]
    public async Task<ActionResult<IReadOnlyList<ServiceHistoryEntry>>> ListHistoryAsync(Guid vehicleId, CancellationToken cancellationToken)
    {
        try
        {
            var history = await maintenance.ListHistoryAsync(vehicleId, cancellationToken);
            return Ok(history);
        }
        catch (DomainException ex)
        {
            return BadRequest(new ApiErrorResponse(ex.Message));
        }
    }

    [HttpGet("maintenance/history/{recordId:guid}")]
    public async Task<ActionResult<ServiceRecordDetailResponse>> GetServiceRecordAsync(Guid recordId, CancellationToken cancellationToken)
    {
        var record = await maintenance.GetServiceRecordAsync(recordId, cancellationToken);
        if (record is null)
        {
            return NotFound();
        }

        return Ok(new ServiceRecordDetailResponse(
            record.Id,
            record.Date,
            record.Odometer,
            record.Summary,
            record.Category,
            record.TotalCost,
            record.PartsCost,
            record.LaborCost,
            record.Shop,
            record.Notes,
            [.. record.Items.Select(i => new ServiceRecordItemResponse(i.Name))],
            [.. record.Receipts.Select(r => new ServiceReceiptResponse(
                r.Title,
                r.StoragePath,
                r.IsImage,
                files.GetUrl(r.StoragePath)))]));
    }
}
