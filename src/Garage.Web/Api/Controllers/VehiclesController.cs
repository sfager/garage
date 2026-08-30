using Garage.Application.Abstractions;
using Garage.Application.Vehicles;
using Garage.Domain.Common;
using Garage.Web.Api.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Garage.Web.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public class VehiclesController(VehicleService vehicles, IFileStore files) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<VehicleSummary>>> ListAsync(CancellationToken cancellationToken)
    {
        var result = await vehicles.ListAllAsync(cancellationToken);
        return Ok(result);
    }

    [HttpGet("{vehicleId:guid}")]
    public async Task<ActionResult<VehicleDetailResponse>> GetAsync(Guid vehicleId, CancellationToken cancellationToken)
    {
        var vehicle = await vehicles.GetAsync(vehicleId, cancellationToken);
        if (vehicle is null)
        {
            return NotFound();
        }

        return Ok(ToDetail(vehicle, files));
    }

    [HttpPost]
    public async Task<ActionResult<VehicleSummary>> AddAsync([FromBody] AddVehicleRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var vehicle = await vehicles.AddAsync(request, cancellationToken);
            var summary = VehicleSummary.From(vehicle);
            return CreatedAtAction(nameof(GetAsync), new { vehicleId = vehicle.Id }, summary);
        }
        catch (DomainException ex)
        {
            return BadRequest(new ApiErrorResponse(ex.Message));
        }
    }

    [HttpPut("{vehicleId:guid}")]
    public async Task<IActionResult> UpdateAsync(Guid vehicleId, [FromBody] EditVehicleRequest request, CancellationToken cancellationToken)
    {
        try
        {
            request.Id = vehicleId;
            await vehicles.UpdateAsync(request, cancellationToken);
            return NoContent();
        }
        catch (DomainException ex)
        {
            return BadRequest(new ApiErrorResponse(ex.Message));
        }
    }

    [HttpPost("{vehicleId:guid}/archive")]
    public async Task<IActionResult> ArchiveAsync(Guid vehicleId, CancellationToken cancellationToken)
    {
        try
        {
            await vehicles.ArchiveAsync(vehicleId, cancellationToken);
            return NoContent();
        }
        catch (DomainException ex)
        {
            return BadRequest(new ApiErrorResponse(ex.Message));
        }
    }

    [HttpPost("{vehicleId:guid}/restore")]
    public async Task<IActionResult> RestoreAsync(Guid vehicleId, CancellationToken cancellationToken)
    {
        try
        {
            await vehicles.RestoreAsync(vehicleId, cancellationToken);
            return NoContent();
        }
        catch (DomainException ex)
        {
            return BadRequest(new ApiErrorResponse(ex.Message));
        }
    }

    [HttpGet("{vehicleId:guid}/deletion-impact")]
    public async Task<IActionResult> GetDeletionImpactAsync(Guid vehicleId, CancellationToken cancellationToken)
    {
        var impact = await vehicles.GetDeletionImpactAsync(vehicleId, cancellationToken);
        return impact is null ? NotFound() : Ok(impact);
    }

    [HttpDelete("{vehicleId:guid}")]
    public async Task<IActionResult> DeleteAsync(Guid vehicleId, CancellationToken cancellationToken)
    {
        try
        {
            await vehicles.DeleteAsync(vehicleId, cancellationToken);
            return NoContent();
        }
        catch (DomainException ex)
        {
            return BadRequest(new ApiErrorResponse(ex.Message));
        }
    }

    [HttpPost("{vehicleId:guid}/photo")]
    public async Task<IActionResult> SetPhotoAsync(Guid vehicleId, IFormFile? file, CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
        {
            return BadRequest(new ApiErrorResponse("Choose an image file."));
        }

        try
        {
            await using var stream = file.OpenReadStream();
            await vehicles.SetPhotoAsync(vehicleId, stream, file.FileName, cancellationToken);
            return NoContent();
        }
        catch (DomainException ex)
        {
            return BadRequest(new ApiErrorResponse(ex.Message));
        }
    }

    [HttpDelete("{vehicleId:guid}/photo")]
    public async Task<IActionResult> RemovePhotoAsync(Guid vehicleId, CancellationToken cancellationToken)
    {
        try
        {
            await vehicles.RemovePhotoAsync(vehicleId, cancellationToken);
            return NoContent();
        }
        catch (DomainException ex)
        {
            return BadRequest(new ApiErrorResponse(ex.Message));
        }
    }

    private static VehicleDetailResponse ToDetail(Garage.Domain.Entities.Vehicle vehicle, IFileStore files) => new(
        vehicle.Id,
        vehicle.Nickname,
        vehicle.Year,
        vehicle.Make,
        vehicle.Model,
        vehicle.Trim,
        vehicle.Engine,
        vehicle.Vin,
        vehicle.LicensePlate,
        vehicle.PhotoPath,
        vehicle.PhotoPath is null ? null : files.GetUrl(vehicle.PhotoPath));
}
