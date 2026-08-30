namespace Garage.Web.Api.Contracts;

public record VehicleDetailResponse(
    Guid Id,
    string Nickname,
    int? Year,
    string? Make,
    string? Model,
    string? Trim,
    string? Engine,
    string? Vin,
    string? LicensePlate,
    string? PhotoPath,
    string? PhotoUrl);
