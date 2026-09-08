using Garage.Application.Mileage;
using Garage.Domain.Common;
using Garage.Domain.Services;
using Garage.Web.Api.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Garage.Web.Api.Controllers;

[ApiController]
[Authorize]
[Route("api")]
public class MileageController(MileageService mileage) : ControllerBase
{
    [HttpGet("vehicles/{vehicleId:guid}/mileage/log")]
    public async Task<ActionResult<IReadOnlyList<MileageLogEntry>>> GetLogAsync(
        Guid vehicleId,
        [FromQuery] MileageEntryKind? filter,
        CancellationToken cancellationToken)
    {
        try
        {
            var log = await mileage.GetLogAsync(vehicleId, filter, cancellationToken);
            return Ok(log);
        }
        catch (DomainException ex)
        {
            return BadRequest(new ApiErrorResponse(ex.Message));
        }
    }

    [HttpGet("vehicles/{vehicleId:guid}/mileage/summary")]
    public async Task<ActionResult<MileageSummary>> GetSummaryAsync(Guid vehicleId, CancellationToken cancellationToken)
    {
        try
        {
            var summary = await mileage.GetSummaryAsync(vehicleId, cancellationToken);
            return Ok(summary);
        }
        catch (DomainException ex)
        {
            return BadRequest(new ApiErrorResponse(ex.Message));
        }
    }

    [HttpPost("vehicles/{vehicleId:guid}/mileage/readings")]
    public async Task<IActionResult> RecordReadingAsync(
        Guid vehicleId,
        [FromBody] RecordReadingRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            await mileage.RecordReadingAsync(vehicleId, request, cancellationToken);
            return NoContent();
        }
        catch (DomainException ex)
        {
            return BadRequest(new ApiErrorResponse(ex.Message));
        }
    }

    [HttpPost("vehicles/{vehicleId:guid}/mileage/trips")]
    public async Task<IActionResult> RecordTripAsync(
        Guid vehicleId,
        [FromBody] RecordTripRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            await mileage.RecordTripAsync(vehicleId, request, cancellationToken);
            return NoContent();
        }
        catch (DomainException ex)
        {
            return BadRequest(new ApiErrorResponse(ex.Message));
        }
    }
}
