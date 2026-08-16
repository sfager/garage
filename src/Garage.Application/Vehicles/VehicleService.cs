using Garage.Application.Abstractions;
using Garage.Domain.Common;
using Garage.Domain.Entities;
using Garage.Domain.Repositories;

namespace Garage.Application.Vehicles;

/// <summary>
/// Epic E1. Everything that changes the shape of the garage: adding a car, correcting
/// its details, archiving it, or removing it altogether.
/// </summary>
public class VehicleService(
    IVehicleRepository vehicles,
    ICurrentUser currentUser,
    IUnitOfWork unitOfWork,
    IClock clock,
    IFileStore fileStore)
{
    /// <summary>
    /// Story V-2: saving creates the vehicle and makes it the selected one. The caller
    /// reloads the <see cref="VehicleContext"/> to pick that up.
    /// </summary>
    public async Task<Vehicle> AddAsync(AddVehicleRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.Odometer is not { } odometer)
        {
            throw new DomainException("Enter today's odometer reading.");
        }

        var householdId = await currentUser.GetHouseholdIdAsync(cancellationToken);

        var vin = Normalize(request.Vin);
        if (vin is not null && await vehicles.VinExistsAsync(vin, householdId, cancellationToken))
        {
            throw new DomainException("A vehicle with that VIN is already in your garage.");
        }

        var vehicle = new Vehicle(householdId, request.Nickname, odometer, clock.Today);
        vehicle.SetDetails(request.Year, request.Make, request.Model, request.Trim, request.Engine, vin, request.LicensePlate);

        await vehicles.AddAsync(vehicle, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return vehicle;
    }

    public async Task<Vehicle?> GetAsync(Guid vehicleId, CancellationToken cancellationToken = default)
    {
        var householdId = await currentUser.GetHouseholdIdAsync(cancellationToken);
        return await vehicles.GetForHouseholdAsync(vehicleId, householdId, cancellationToken);
    }

    /// <summary>Every vehicle the household owns, archived ones included.</summary>
    public async Task<IReadOnlyList<VehicleSummary>> ListAllAsync(CancellationToken cancellationToken = default)
    {
        var householdId = await currentUser.GetHouseholdIdAsync(cancellationToken);
        var all = await vehicles.ListAllAsync(householdId, cancellationToken);
        return [.. all.Select(VehicleSummary.From)];
    }

    public async Task UpdateAsync(EditVehicleRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var vehicle = await RequireAsync(request.Id, cancellationToken);

        var vin = Normalize(request.Vin);
        if (vin is not null && !string.Equals(vin, vehicle.Vin, StringComparison.OrdinalIgnoreCase))
        {
            var householdId = await currentUser.GetHouseholdIdAsync(cancellationToken);
            if (await vehicles.VinExistsAsync(vin, householdId, cancellationToken))
            {
                throw new DomainException("Another vehicle in your garage already has that VIN.");
            }
        }

        vehicle.Rename(request.Nickname);
        vehicle.SetDetails(request.Year, request.Make, request.Model, request.Trim, request.Engine, vin, request.LicensePlate);

        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    /// <summary>Replaces the vehicle photo, deleting whatever it had before.</summary>
    public async Task SetPhotoAsync(Guid vehicleId, Stream content, string fileName, CancellationToken cancellationToken = default)
    {
        var vehicle = await RequireAsync(vehicleId, cancellationToken);
        var previous = vehicle.PhotoPath;

        var key = await fileStore.SaveAsync(content, fileName, "vehicles", cancellationToken);
        vehicle.SetPhoto(key);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        if (previous is not null)
        {
            await fileStore.DeleteAsync(previous, cancellationToken);
        }
    }

    public async Task RemovePhotoAsync(Guid vehicleId, CancellationToken cancellationToken = default)
    {
        var vehicle = await RequireAsync(vehicleId, cancellationToken);
        var previous = vehicle.PhotoPath;

        vehicle.SetPhoto(null);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        if (previous is not null)
        {
            await fileStore.DeleteAsync(previous, cancellationToken);
        }
    }

    /// <summary>
    /// Story V-4: archiving takes the vehicle out of the switcher but leaves every
    /// record in place, so reports over past years still add up.
    /// </summary>
    public async Task ArchiveAsync(Guid vehicleId, CancellationToken cancellationToken = default)
    {
        var vehicle = await RequireAsync(vehicleId, cancellationToken);
        vehicle.Archive();
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task RestoreAsync(Guid vehicleId, CancellationToken cancellationToken = default)
    {
        var vehicle = await RequireAsync(vehicleId, cancellationToken);
        vehicle.Restore();
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task<VehicleDeletionImpact?> GetDeletionImpactAsync(Guid vehicleId, CancellationToken cancellationToken = default)
    {
        var householdId = await currentUser.GetHouseholdIdAsync(cancellationToken);
        return await vehicles.GetDeletionImpactAsync(vehicleId, householdId, cancellationToken);
    }

    /// <summary>Removes the vehicle and everything hanging off it. Not reversible.</summary>
    public async Task DeleteAsync(Guid vehicleId, CancellationToken cancellationToken = default)
    {
        var vehicle = await RequireAsync(vehicleId, cancellationToken);
        var photo = vehicle.PhotoPath;

        vehicles.Remove(vehicle);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        if (photo is not null)
        {
            await fileStore.DeleteAsync(photo, cancellationToken);
        }
    }

    private async Task<Vehicle> RequireAsync(Guid vehicleId, CancellationToken cancellationToken)
    {
        var householdId = await currentUser.GetHouseholdIdAsync(cancellationToken);
        return await vehicles.GetForHouseholdAsync(vehicleId, householdId, cancellationToken)
            ?? throw new DomainException("That vehicle is not in your garage.");
    }

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim().ToUpperInvariant();
}
