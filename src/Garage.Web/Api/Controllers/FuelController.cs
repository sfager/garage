using Garage.Application.Fuel;
using Garage.Domain.Common;
using Garage.Web.Api.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Garage.Web.Api.Controllers;

[ApiController]
[Authorize]
[Route("api")]
public class FuelController(FuelService fuel) : ControllerBase
{
    [HttpGet("vehicles/{vehicleId:guid}/fuel/screen")]
    public async Task<ActionResult<FuelScreen>> GetScreenAsync(
        Guid vehicleId,
        [FromQuery] FuelRange range = FuelRange.SixMonths,
        [FromQuery] FuelMetric metric = FuelMetric.Mpg,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var screen = await fuel.GetScreenAsync(vehicleId, range, metric, cancellationToken);
            return Ok(screen);
        }
        catch (DomainException ex)
        {
            return BadRequest(new ApiErrorResponse(ex.Message));
        }
    }

    [HttpPost("vehicles/{vehicleId:guid}/fuel/entries")]
    public async Task<IActionResult> SaveAsync(
        Guid vehicleId,
        [FromBody] FuelEntryRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            await fuel.SaveAsync(vehicleId, request, cancellationToken);
            return NoContent();
        }
        catch (DomainException ex)
        {
            return BadRequest(new ApiErrorResponse(ex.Message));
        }
    }

    [HttpDelete("fuel/entries/{entryId:guid}")]
    public async Task<IActionResult> DeleteAsync(Guid entryId, CancellationToken cancellationToken)
    {
        try
        {
            await fuel.DeleteAsync(entryId, cancellationToken);
            return NoContent();
        }
        catch (DomainException ex)
        {
            return BadRequest(new ApiErrorResponse(ex.Message));
        }
    }

    [HttpGet("fuel/stations")]
    public async Task<ActionResult<IReadOnlyList<string>>> ListStationsAsync(CancellationToken cancellationToken)
    {
        var stations = await fuel.ListStationsAsync(cancellationToken);
        return Ok(stations);
    }
}
