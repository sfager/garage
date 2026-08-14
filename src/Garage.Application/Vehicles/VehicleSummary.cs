using Garage.Domain.Entities;

namespace Garage.Application.Vehicles;

/// <summary>What the switcher and any vehicle-header need, without the aggregate behind it.</summary>
public record VehicleSummary(
    Guid Id,
    string Nickname,
    string DisplayName,
    int CurrentOdometer,
    DateOnly CurrentOdometerDate,
    string? PhotoPath,
    bool IsArchived)
{
    public static VehicleSummary From(Vehicle vehicle) => new(
        vehicle.Id,
        vehicle.Nickname,
        vehicle.DisplayName,
        vehicle.CurrentOdometer,
        vehicle.CurrentOdometerDate,
        vehicle.PhotoPath,
        vehicle.IsArchived);
}
