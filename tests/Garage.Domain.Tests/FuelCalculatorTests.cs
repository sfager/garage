using Garage.Domain.Services;
using NUnit.Framework;

namespace Garage.Domain.Tests;

[TestFixture]
public class FuelCalculatorTests
{
    private static FuelFill Fill(int odometer, decimal gallons, bool partial = false, decimal cost = 40m, int day = 1) =>
        new(Guid.NewGuid(), new DateOnly(2026, 7, day), odometer, gallons, cost, partial);

    [Test]
    public void TestCalculate_WhenThereAreNoFills_SaysNothingIsLogged()
    {
        // Arrange
        var fills = Array.Empty<FuelFill>();

        // Act
        var result = FuelCalculator.Calculate(fills);

        // Assert
        Assert.That(result.AverageMpg, Is.Null);
        Assert.That(result.UnavailableReason, Is.EqualTo("No fill-ups logged yet."));
    }

    [Test]
    public void TestCalculate_WhenOnlyOneFullFillExists_AsksForASecond()
    {
        // Arrange — the first full fill only establishes the baseline.
        FuelFill[] fills = [Fill(87_320, 11.9m)];

        // Act
        var result = FuelCalculator.Calculate(fills);

        // Assert
        Assert.That(result.AverageMpg, Is.Null);
        Assert.That(result.UnavailableReason, Is.EqualTo("Log a second full fill-up to see miles per gallon."));
        Assert.That(result.Fills.Single().Mpg, Is.Null);
    }

    [Test]
    public void TestCalculate_WhenEveryFillIsPartial_SaysThereIsNoFullTankToMeasureBetween()
    {
        // Arrange
        FuelFill[] fills = [Fill(87_320, 5m, partial: true), Fill(87_600, 6m, partial: true)];

        // Act
        var result = FuelCalculator.Calculate(fills);

        // Assert
        Assert.That(result.UnavailableReason,
            Is.EqualTo("Every fill-up so far is a partial, so there is no full tank to measure between."));
    }

    [Test]
    public void TestCalculate_WhenTwoFullFillsBracketADistance_DividesMilesByGallons()
    {
        // Arrange — 320 miles on the 11.9 gallons it took to refill.
        FuelFill[] fills = [Fill(87_320, 11.9m), Fill(87_640, 11.9m)];

        // Act
        var result = FuelCalculator.Calculate(fills);

        // Assert
        Assert.That(result.AverageMpg, Is.EqualTo(320 / 11.9).Within(0.01));
        Assert.That(result.Fills[0].Mpg, Is.EqualTo(320 / 11.9).Within(0.01));
        Assert.That(result.Fills[0].MilesCovered, Is.EqualTo(320));
    }

    [Test]
    public void TestCalculate_WhenAPartialFillSitsBetweenFullOnes_CountsItsGallonsButGivesItNoMpg()
    {
        // Arrange — story G-2: partial fills are excluded from the calculation as their
        // own data point, but the fuel they added still propelled the car.
        FuelFill[] fills =
        [
            Fill(88_000, 10m),
            Fill(88_150, 5m, partial: true),
            Fill(88_300, 5m)
        ];

        // Act
        var result = FuelCalculator.Calculate(fills);

        // Assert — 300 miles on the 10 gallons added since the last full tank.
        var partial = result.Fills.Single(f => f.IsPartialFill);
        var closing = result.Fills.Single(f => f.Odometer == 88_300);

        Assert.That(partial.Mpg, Is.Null);
        Assert.That(closing.Mpg, Is.EqualTo(30).Within(0.001));
        Assert.That(result.AverageMpg, Is.EqualTo(30).Within(0.001));
    }

    [Test]
    public void TestCalculate_WhenSeveralIntervalsExist_AveragesTotalMilesOverTotalGallons()
    {
        // Arrange — a straight mean of per-fill figures would weight a short tank the
        // same as a long one, so the average aggregates instead.
        FuelFill[] fills =
        [
            Fill(88_000, 10m),
            Fill(88_400, 10m),   // 400 / 10 = 40 mpg
            Fill(88_500, 5m)     // 100 / 5  = 20 mpg
        ];

        // Act
        var result = FuelCalculator.Calculate(fills);

        // Assert — 500 miles on 15 gallons is 33.3, not the 30 a plain mean would give.
        Assert.That(result.AverageMpg, Is.EqualTo(500 / 15.0).Within(0.001));
        Assert.That(result.MilesMeasured, Is.EqualTo(500));
        Assert.That(result.GallonsMeasured, Is.EqualTo(15m));
    }

    [Test]
    public void TestCalculate_WhenFillsArriveOutOfOrder_StillPairsThemByOdometer()
    {
        // Arrange
        FuelFill[] fills = [Fill(88_400, 10m, day: 20), Fill(88_000, 10m, day: 5)];

        // Act
        var result = FuelCalculator.Calculate(fills);

        // Assert
        Assert.That(result.AverageMpg, Is.EqualTo(40).Within(0.001));
    }

    [Test]
    public void TestCalculate_WhenTwoFillsShareAnOdometer_IgnoresTheZeroDistance()
    {
        // Arrange — topping up twice at the same reading covers no ground.
        FuelFill[] fills = [Fill(88_000, 10m), Fill(88_000, 2m)];

        // Act
        var result = FuelCalculator.Calculate(fills);

        // Assert
        Assert.That(result.AverageMpg, Is.Null);
        Assert.That(result.UnavailableReason, Is.EqualTo("The fill-ups logged do not cover any distance yet."));
    }

    [Test]
    public void TestCalculate_WhenListed_ReturnsNewestFirst()
    {
        // Arrange
        FuelFill[] fills = [Fill(88_000, 10m, day: 1), Fill(88_400, 10m, day: 20)];

        // Act
        var result = FuelCalculator.Calculate(fills);

        // Assert
        Assert.That(result.Fills[0].Odometer, Is.EqualTo(88_400));
    }
}
