using Garage.Domain;
using Garage.Domain.Repositories;
using Garage.Domain.Services;
using NUnit.Framework;

namespace Garage.Domain.Tests;

[TestFixture]
public class ReportCalculatorTests
{
    private static readonly Guid Outback = Guid.NewGuid();
    private static readonly Guid Fit = Guid.NewGuid();

    private static CostLine Service(decimal cost, string item, ServiceCategory category, int month = 3, Guid? vehicle = null) =>
        new(vehicle ?? Outback, vehicle == Fit ? "Fit" : "Outback", Guid.NewGuid(),
            new DateOnly(2026, month, 2), 82_100, CostKind.Service, category, item, "Tire Depot", cost);

    private static CostLine Fuel(decimal cost, int month = 7, Guid? vehicle = null) =>
        new(vehicle ?? Outback, vehicle == Fit ? "Fit" : "Outback", Guid.NewGuid(),
            new DateOnly(2026, month, 9), 88_290, CostKind.Fuel, ServiceCategory.Other, "11.9 gal", "Shell", cost);

    private static OdometerPoint Point(int odometer, int month, Guid? vehicle = null) =>
        new(vehicle ?? Outback, new DateOnly(2026, month, 1), odometer);

    [Test]
    public void TestBuildDashboard_WhenGivenServiceAndFuel_SumsBothIntoTotalSpend()
    {
        // Arrange
        CostLine[] lines = [Service(740m, "Tires ×4", ServiceCategory.Tires), Fuel(41.60m)];

        // Act
        var dashboard = ReportCalculator.BuildDashboard(lines, []);

        // Assert
        Assert.That(dashboard.TotalSpend, Is.EqualTo(781.60m));
    }

    [Test]
    public void TestBuildDashboard_WhenMilesAreKnown_DividesTotalSpendByThem()
    {
        // Arrange — 1m defines cost per mile as everything spent over the distance covered.
        CostLine[] lines = [Service(500m, "Service", ServiceCategory.ScheduledService)];
        OdometerPoint[] points = [Point(80_000, 1), Point(82_000, 6)];

        // Act
        var dashboard = ReportCalculator.BuildDashboard(lines, points);

        // Assert
        Assert.That(dashboard.MilesDriven, Is.EqualTo(2_000));
        Assert.That(dashboard.CostPerMile, Is.EqualTo(0.25m));
    }

    [Test]
    public void TestBuildDashboard_WhenNoMileageIsRecorded_LeavesCostPerMileUnset()
    {
        // Arrange
        CostLine[] lines = [Service(500m, "Service", ServiceCategory.ScheduledService)];

        // Act
        var dashboard = ReportCalculator.BuildDashboard(lines, []);

        // Assert — a zero here would read as "this car costs nothing to run".
        Assert.That(dashboard.CostPerMile, Is.Null);
        Assert.That(dashboard.MilesDriven, Is.Zero);
    }

    [Test]
    public void TestMilesDriven_WhenTwoVehiclesAreMixed_MeasuresEachSeparately()
    {
        // Arrange — the Fit reads 142,000 while the Outback reads 82,000; the gap between
        // them is not distance anybody drove.
        OdometerPoint[] points =
        [
            Point(82_000, 1), Point(83_000, 6),
            Point(142_000, 1, Fit), Point(143_000, 6, Fit)
        ];

        // Act
        var miles = ReportCalculator.MilesDriven(points);

        // Assert
        Assert.That(miles, Is.EqualTo(2_000));
    }

    [Test]
    public void TestBuildDashboard_WhenSpendVaries_NamesTheLargestLineItem()
    {
        // Arrange
        CostLine[] lines =
        [
            Service(740m, "Tires ×4", ServiceCategory.Tires),
            Service(68m, "Oil & filter", ServiceCategory.ScheduledService),
            Fuel(44m)
        ];

        // Act
        var dashboard = ReportCalculator.BuildDashboard(lines, []);

        // Assert
        Assert.That(dashboard.LargestLineItem, Is.EqualTo("Tires ×4"));
        Assert.That(dashboard.LargestLineAmount, Is.EqualTo(740m));
    }

    [Test]
    public void TestBuildDashboard_WhenGroupedByMonth_SplitsServiceFromFuel()
    {
        // Arrange
        CostLine[] lines =
        [
            Service(740m, "Tires ×4", ServiceCategory.Tires, month: 3),
            Fuel(44m, month: 3),
            Fuel(41m, month: 7)
        ];

        // Act
        var dashboard = ReportCalculator.BuildDashboard(lines, []);

        // Assert
        var march = dashboard.ByMonth.Single(m => m.Month.Month == 3);
        Assert.That(march.Service, Is.EqualTo(740m));
        Assert.That(march.Fuel, Is.EqualTo(44m));
        Assert.That(march.Total, Is.EqualTo(784m));
        Assert.That(dashboard.ByMonth, Has.Count.EqualTo(2));
    }

    [Test]
    public void TestBuildDashboard_WhenBrokenDownByCategory_PutsFuelInItsOwnRowAndSortsByAmount()
    {
        // Arrange
        CostLine[] lines =
        [
            Service(740m, "Tires ×4", ServiceCategory.Tires),
            Service(620m, "Scheduled", ServiceCategory.ScheduledService),
            Fuel(1_940m)
        ];

        // Act
        var dashboard = ReportCalculator.BuildDashboard(lines, []);

        // Assert
        Assert.That(dashboard.ByCategory[0].Category, Is.EqualTo("Fuel"));
        Assert.That(dashboard.ByCategory[0].ShareOfLargest, Is.EqualTo(1).Within(0.001));
        Assert.That(dashboard.ByCategory[1].Category, Is.EqualTo("Tires"));
        Assert.That(dashboard.ByCategory[1].ShareOfLargest, Is.EqualTo(740d / 1_940d).Within(0.001));
    }

    [Test]
    public void TestCompare_WhenRangeIsHalfAYear_AnnualisesSpendSoVehiclesLineUp()
    {
        // Arrange — story R-3.
        CostLine[] lines = [Service(500m, "Service", ServiceCategory.ScheduledService)];
        OdometerPoint[] points = [Point(80_000, 1), Point(85_000, 6)];

        // Act
        var comparison = ReportCalculator.Compare(lines, points, new Dictionary<Guid, double?>(), daysInRange: 182);

        // Assert — 500 over half a year is roughly 1,000 a year.
        Assert.That(comparison.Single().AnnualisedSpend, Is.EqualTo(500m * 365m / 182m).Within(0.01m));
    }

    [Test]
    public void TestCompare_WhenTwoVehiclesHaveData_ReturnsBothWithTheirOwnFigures()
    {
        // Arrange
        CostLine[] lines =
        [
            Service(500m, "Service", ServiceCategory.ScheduledService),
            Service(412m, "Alternator", ServiceCategory.Repair, vehicle: Fit)
        ];
        OdometerPoint[] points = [Point(80_000, 1), Point(85_000, 6), Point(142_000, 1, Fit), Point(143_000, 6, Fit)];
        var mpg = new Dictionary<Guid, double?> { [Outback] = 27.1, [Fit] = 33.8 };

        // Act
        var comparison = ReportCalculator.Compare(lines, points, mpg, daysInRange: 365);

        // Assert
        var outback = comparison.Single(c => c.VehicleId == Outback);
        var fit = comparison.Single(c => c.VehicleId == Fit);

        Assert.That(outback.CostPerMile, Is.EqualTo(0.10m));
        Assert.That(outback.AverageMpg, Is.EqualTo(27.1));
        Assert.That(fit.CostPerMile, Is.EqualTo(0.412m));
        Assert.That(fit.MilesDriven, Is.EqualTo(1_000));
    }
}
