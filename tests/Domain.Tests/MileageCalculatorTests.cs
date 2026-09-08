using Garage.Domain.Services;
using NUnit.Framework;

namespace Garage.Domain.Tests;

[TestFixture]
public class MileageCalculatorTests
{
    private static readonly DateOnly Today = new(2026, 8, 13);

    [Test]
    public void TestSummarize_WhenThereAreNoPoints_SaysNothingIsRecorded()
    {
        // Arrange
        var points = Array.Empty<MileagePoint>();

        // Act
        var summary = MileageCalculator.Summarize(points, Today);

        // Assert
        Assert.That(summary.HasAverage, Is.False);
        Assert.That(summary.UnavailableReason, Is.EqualTo("No mileage recorded yet."));
    }

    [Test]
    public void TestSummarize_WhenTripsSitBetweenTwoReadings_MeasuresFromThePreviousReading()
    {
        // Arrange — the wireframe's case [1j]: two trips between the Aug 1 and Aug 13
        // readings are the miles being counted, not marks to count from.
        MileagePoint[] points =
        [
            new(new DateOnly(2026, 8, 1), 88_000, IsReading: true),
            new(new DateOnly(2026, 8, 6), 88_126),
            new(new DateOnly(2026, 8, 11), 88_404),
            new(new DateOnly(2026, 8, 13), 88_412, IsReading: true)
        ];

        // Act
        var summary = MileageCalculator.Summarize(points, Today);

        // Assert
        Assert.That(summary.MilesSinceLast, Is.EqualTo(412));
        Assert.That(summary.SinceDate, Is.EqualTo(new DateOnly(2026, 8, 1)));
    }

    [Test]
    public void TestSummarize_WhenTheNewestPointIsATrip_MeasuresFromTheLatestReading()
    {
        // Arrange
        MileagePoint[] points =
        [
            new(new DateOnly(2026, 8, 1), 88_000, IsReading: true),
            new(new DateOnly(2026, 8, 11), 88_404)
        ];

        // Act
        var summary = MileageCalculator.Summarize(points, Today);

        // Assert
        Assert.That(summary.MilesSinceLast, Is.EqualTo(404));
        Assert.That(summary.SinceDate, Is.EqualTo(new DateOnly(2026, 8, 1)));
    }

    [Test]
    public void TestSummarize_WhenNoEarlierReadingExists_LeavesMilesSinceLastUnset()
    {
        // Arrange — one reading and nothing before it to measure from.
        MileagePoint[] points =
        [
            new(new DateOnly(2026, 8, 1), 88_000, IsReading: true),
            new(new DateOnly(2026, 8, 13), 88_412, IsReading: true)
        ];

        // Act
        var summary = MileageCalculator.Summarize([points[0]], Today);

        // Assert
        Assert.That(summary.MilesSinceLast, Is.Null);
        Assert.That(summary.SinceDate, Is.Null);
    }

    [Test]
    public void TestSummarize_WhenThereIsOnlyOnePoint_CannotProduceAnAverage()
    {
        // Arrange
        MileagePoint[] points = [new(new DateOnly(2026, 8, 1), 88_000, IsReading: true)];

        // Act
        var summary = MileageCalculator.Summarize(points, Today);

        // Assert
        Assert.That(summary.CurrentOdometer, Is.EqualTo(88_000));
        Assert.That(summary.MilesSinceLast, Is.Null);
        Assert.That(summary.UnavailableReason, Is.EqualTo("Add another reading to see a daily average."));
    }

    [Test]
    public void TestSummarize_WhenAllPointsShareOneDay_StatesThereIsNoRateYet()
    {
        // Arrange — story G-2's principle applied here: state the reason, never zero.
        MileagePoint[] points =
        [
            new(Today, 88_000),
            new(Today, 88_060)
        ];

        // Act
        var summary = MileageCalculator.Summarize(points, Today);

        // Assert
        Assert.That(summary.MilesPerDay, Is.Null);
        Assert.That(summary.UnavailableReason, Is.EqualTo("All readings are from the same day, so there is no rate yet."));
    }

    [Test]
    public void TestSummarize_WhenPointsSpanDays_DividesDistanceByElapsedDays()
    {
        // Arrange — 400 miles across 10 days is 40 a day.
        MileagePoint[] points =
        [
            new(new DateOnly(2026, 8, 3), 88_000),
            new(new DateOnly(2026, 8, 13), 88_400)
        ];

        // Act
        var summary = MileageCalculator.Summarize(points, Today);

        // Assert
        Assert.That(summary.MilesPerDay, Is.EqualTo(40).Within(0.001));
        Assert.That(summary.DaysCovered, Is.EqualTo(10));
    }

    [Test]
    public void TestSummarize_WhenPointsArriveOutOfOrder_StillReadsTheNewestAsCurrent()
    {
        // Arrange
        MileagePoint[] points =
        [
            new(new DateOnly(2026, 8, 13), 88_412, IsReading: true),
            new(new DateOnly(2026, 8, 1), 88_000, IsReading: true),
            new(new DateOnly(2026, 8, 6), 88_126)
        ];

        // Act
        var summary = MileageCalculator.Summarize(points, Today);

        // Assert
        Assert.That(summary.CurrentOdometer, Is.EqualTo(88_412));
        Assert.That(summary.MilesSinceLast, Is.EqualTo(412));
        Assert.That(summary.SinceDate, Is.EqualTo(new DateOnly(2026, 8, 1)));
    }

    [Test]
    public void TestSummarize_WhenOlderHistoryFallsOutsideTheWindow_AveragesOnlyRecentUse()
    {
        // Arrange — a long-idle year followed by heavy recent use must not average flat.
        MileagePoint[] points =
        [
            new(new DateOnly(2024, 1, 1), 40_000),
            new(new DateOnly(2026, 7, 14), 88_000),
            new(new DateOnly(2026, 8, 13), 89_500)
        ];

        // Act
        var summary = MileageCalculator.Summarize(points, Today, windowDays: 90);

        // Assert — 1,500 miles over the 30 days inside the window.
        Assert.That(summary.MilesPerDay, Is.EqualTo(50).Within(0.001));
        Assert.That(summary.DaysCovered, Is.EqualTo(30));
    }

    [Test]
    public void TestSummarize_WhenTheWindowHoldsTooLittle_WidensToTheWholeHistory()
    {
        // Arrange — only one point falls inside the window, so the window alone says nothing.
        MileagePoint[] points =
        [
            new(new DateOnly(2026, 1, 3), 80_000),
            new(new DateOnly(2026, 8, 13), 88_000)
        ];

        // Act
        var summary = MileageCalculator.Summarize(points, Today, windowDays: 30);

        // Assert
        Assert.That(summary.HasAverage, Is.True);
        Assert.That(summary.DaysCovered, Is.EqualTo(222));
    }

    [TestCase(1_100, 40.0, 28)]
    [TestCase(0, 40.0, 0)]
    [TestCase(100, 33.0, 4)]
    public void TestDaysToCover_WithARate_RoundsUpToWholeDays(int miles, double rate, int expected)
    {
        // Arrange
        // (the distance and rate come from the test case)

        // Act
        var days = MileageCalculator.DaysToCover(miles, rate);

        // Assert
        Assert.That(days, Is.EqualTo(expected));
    }

    [TestCase(null)]
    [TestCase(0.0)]
    public void TestDaysToCover_WithoutAUsableRate_ReturnsNull(double? rate)
    {
        // Arrange
        var miles = 1_000;

        // Act
        var days = MileageCalculator.DaysToCover(miles, rate);

        // Assert
        Assert.That(days, Is.Null);
    }
}
