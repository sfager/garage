using Garage.Domain;
using Garage.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Garage.Infrastructure.Persistence;

/// <summary>
/// Story F-1: seed data for development. Fills an empty household with the two cars
/// from the wireframes — a 2019 Outback and a Honda Fit — on one consistent mileage
/// spine, so every screen has something real to render before any data is entered.
/// </summary>
public class GarageDbSeeder(GarageDbContext context, ILogger<GarageDbSeeder> logger)
{
    public async Task SeedHouseholdAsync(Guid householdId, CancellationToken cancellationToken = default)
    {
        if (await context.Vehicles.AnyAsync(v => v.HouseholdId == householdId, cancellationToken))
        {
            return;
        }

        logger.LogInformation("Seeding demo vehicles into household {HouseholdId}", householdId);

        var outback = SeedOutback(householdId);
        var fit = SeedFit(householdId);

        context.Vehicles.AddRange(outback, fit);
        await context.SaveChangesAsync(cancellationToken);
    }

    private static Vehicle SeedOutback(Guid householdId)
    {
        // The spine starts well before the first fill-up so every later record moves forward.
        var vehicle = new Vehicle(householdId, "Outback", 82_100, new DateOnly(2026, 3, 2));
        vehicle.SetDetails(2019, "Subaru", "Outback", "2.5i Premium", "2.5L H4", "4S4BSANC1K3311204", "XKD 4417");

        var tires = new ServiceRecord(vehicle.Id, new DateOnly(2026, 3, 2), 82_100, ServiceCategory.Tires, 740.00m);
        tires.AddItem("Tires ×4");
        tires.SetShop("Tire Depot");
        tires.SetCostBreakdown(620.00m, 120.00m);
        vehicle.RecordService(tires);

        var oil = new ServiceRecord(vehicle.Id, new DateOnly(2026, 2, 10), 82_500, ServiceCategory.ScheduledService, 68.00m);
        oil.AddItem("Oil & filter");
        oil.SetShop("Westside Auto");
        oil.SetNotes("0W-20 full synthetic.");
        vehicle.RecordService(oil);

        // Fuel: four full fills, each roughly 320 miles apart, landing near 27 mpg.
        AddFillUp(vehicle, new DateOnly(2026, 7, 3), 87_320, 11.9m, 44.00m, "BP");
        AddFillUp(vehicle, new DateOnly(2026, 7, 16), 87_640, 11.9m, 40.20m, "Shell, Elm St");
        AddFillUp(vehicle, new DateOnly(2026, 7, 28), 87_970, 12.0m, 39.10m, "Costco");

        vehicle.RecordReading(new DateOnly(2026, 8, 1), 88_000);
        vehicle.RecordTrip(Trip.FromOdometers(vehicle.Id, new DateOnly(2026, 8, 6), 88_000, 88_126, "Commute week", TripPurpose.Personal));

        AddFillUp(vehicle, new DateOnly(2026, 8, 9), 88_290, 11.9m, 41.60m, "Shell, Elm St");

        vehicle.RecordTrip(Trip.FromOdometers(vehicle.Id, new DateOnly(2026, 8, 11), 88_290, 88_404, "Home → Portland", TripPurpose.Business));
        vehicle.RecordReading(new DateOnly(2026, 8, 13), 88_412);

        // Anchored at the February oil change, so this reads as 912 miles past due.
        vehicle.AddReminder(new Reminder(vehicle.Id, "Oil & filter", 5_000, 6, 82_500, new DateOnly(2026, 2, 10)));
        vehicle.AddReminder(new Reminder(vehicle.Id, "Tire rotation", 6_000, 6, 83_500, new DateOnly(2026, 3, 2)));
        vehicle.AddReminder(new Reminder(vehicle.Id, "Cabin air filter", null, 12, 82_100, new DateOnly(2025, 12, 15)));
        vehicle.AddReminder(new Reminder(vehicle.Id, "Brake fluid", 30_000, 36, 60_000, new DateOnly(2023, 12, 1)));
        vehicle.AddReminder(new Reminder(vehicle.Id, "Spark plugs", 60_000, null, 45_000, new DateOnly(2022, 6, 1)));
        vehicle.AddReminder(new Reminder(vehicle.Id, "Timing belt", 105_000, null, 15_000, new DateOnly(2019, 4, 1)));

        return vehicle;
    }

    private static Vehicle SeedFit(Guid householdId)
    {
        var vehicle = new Vehicle(householdId, "Fit", 141_900, new DateOnly(2026, 6, 1));
        vehicle.SetDetails(2018, "Honda", "Fit", "LX", "1.5L I4", "3HGGK5H55JM701882", "PLR 9026");

        AddFillUp(vehicle, new DateOnly(2026, 6, 20), 142_240, 10.1m, 33.40m, "Costco");
        AddFillUp(vehicle, new DateOnly(2026, 7, 8), 142_580, 9.9m, 32.10m, "Shell, Elm St");

        var alternator = new ServiceRecord(vehicle.Id, new DateOnly(2026, 7, 22), 142_880, ServiceCategory.Repair, 412.00m);
        alternator.AddItem("Alternator");
        alternator.SetShop("Mike's Garage");
        alternator.SetCostBreakdown(268.00m, 144.00m);
        alternator.SetNotes("Belt tensioner checked at the same time.");
        vehicle.RecordService(alternator);

        AddFillUp(vehicle, new DateOnly(2026, 8, 2), 142_920, 10.2m, 34.80m, "Costco");
        vehicle.RecordReading(new DateOnly(2026, 8, 12), 143_050);

        vehicle.AddReminder(new Reminder(vehicle.Id, "Oil & filter", 5_000, 6, 140_500, new DateOnly(2026, 4, 18)));
        vehicle.AddReminder(new Reminder(vehicle.Id, "Tire rotation", 6_000, null, 138_000, new DateOnly(2026, 1, 9)));
        vehicle.AddReminder(new Reminder(vehicle.Id, "State inspection", null, 12, 138_000, new DateOnly(2026, 3, 1)));

        return vehicle;
    }

    private static void AddFillUp(Vehicle vehicle, DateOnly date, int odometer, decimal gallons, decimal cost, string station)
    {
        var entry = new FuelEntry(vehicle.Id, date, odometer, gallons, cost);
        entry.SetStation(station);
        vehicle.RecordFillUp(entry);
    }
}
