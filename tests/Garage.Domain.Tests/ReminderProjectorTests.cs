using Garage.Domain;
using Garage.Domain.Entities;
using Garage.Domain.Services;
using NUnit.Framework;

namespace Garage.Domain.Tests;

[TestFixture]
public class ReminderProjectorTests
{
    private static readonly Guid VehicleId = Guid.NewGuid();
    private static readonly DateOnly Today = new(2026, 8, 13);

    /// <summary>The Outback's figures from the wireframes: 88,412 miles, ~27 a day.</summary>
    private const int CurrentOdometer = 88_412;
    private const double MilesPerDay = 27.0;

    [Test]
    public void TestProject_WhenTheMileageTriggerHasPassed_BandsAsOverdueAndStatesHowFar()
    {
        // Arrange — oil & filter, 5,000 mi from an 82,500 service, so due at 87,500 [1c].
        var reminder = new Reminder(VehicleId, "Oil & filter", 5_000, 6, 82_500, new DateOnly(2026, 2, 10));

        // Act
        var projection = ReminderProjector.Project(reminder, CurrentOdometer, Today, MilesPerDay);

        // Assert
        Assert.That(projection.Band, Is.EqualTo(DueBand.Overdue));
        Assert.That(projection.MilesRemaining, Is.EqualTo(-912));
        Assert.That(projection.RemainingDescription, Is.EqualTo("912 mi past due"));
    }

    [Test]
    public void TestProject_WhenAMileageTriggerIsAFewMonthsOut_BandsAsDueSoon()
    {
        // Arrange — tire rotation due at 89,500, which is 1,088 miles away [1c]. Its
        // annual trigger is far enough out that mileage is what will fire.
        var reminder = new Reminder(VehicleId, "Tire rotation", 6_000, 12, 83_500, new DateOnly(2026, 3, 2));

        // Act
        var projection = ReminderProjector.Project(reminder, CurrentOdometer, Today, MilesPerDay);

        // Assert
        Assert.That(projection.Band, Is.EqualTo(DueBand.DueSoon));
        Assert.That(projection.LeadingTrigger, Is.EqualTo(DueTrigger.Mileage));
        Assert.That(projection.RemainingDescription, Is.EqualTo("1,088 mi to go"));
    }

    [Test]
    public void TestProject_WhenOnlyAMonthIntervalIsSet_LeadsOnTimeAndCountsMonths()
    {
        // Arrange — cabin air filter, 12 months from December 2025 [1c].
        var reminder = new Reminder(VehicleId, "Cabin air filter", null, 12, 82_100, new DateOnly(2025, 12, 15));

        // Act
        var projection = ReminderProjector.Project(reminder, CurrentOdometer, Today, MilesPerDay);

        // Assert
        Assert.That(projection.Band, Is.EqualTo(DueBand.DueSoon));
        Assert.That(projection.LeadingTrigger, Is.EqualTo(DueTrigger.Time));
        Assert.That(projection.RemainingDescription, Is.EqualTo("4 mo to go"));
    }

    [Test]
    public void TestProject_WhenTheTriggerIsTensOfThousandsOfMilesOut_BandsAsLater()
    {
        // Arrange — spark plugs at 105,000, about 16,600 miles away [1c].
        var reminder = new Reminder(VehicleId, "Spark plugs", 60_000, null, 45_000, new DateOnly(2022, 6, 1));

        // Act
        var projection = ReminderProjector.Project(reminder, CurrentOdometer, Today, MilesPerDay);

        // Assert
        Assert.That(projection.Band, Is.EqualTo(DueBand.Later));
        Assert.That(projection.DueOdometer, Is.EqualTo(105_000));
    }

    [Test]
    public void TestProject_WhenBothTriggersAreSet_LeadsWithWhicheverArrivesFirst()
    {
        // Arrange — brake fluid: due at 100,000 miles is a long way off, but the
        // 36-month trigger is up in four months, so time wins [1a].
        var reminder = new Reminder(VehicleId, "Brake fluid", 30_000, 36, 70_000, new DateOnly(2023, 12, 1));

        // Act
        var projection = ReminderProjector.Project(reminder, CurrentOdometer, Today, MilesPerDay);

        // Assert
        Assert.That(projection.LeadingTrigger, Is.EqualTo(DueTrigger.Time));
        Assert.That(projection.Band, Is.EqualTo(DueBand.DueSoon));
    }

    [Test]
    public void TestProject_WhenTheDailyAverageIsHigher_TheSameMileageTriggerArrivesSooner()
    {
        // Arrange — story S-3: projections follow the real rate, not a fixed guess.
        var reminder = new Reminder(VehicleId, "Tire rotation", 6_000, null, 83_500, new DateOnly(2026, 3, 2));

        // Act
        var slow = ReminderProjector.Project(reminder, CurrentOdometer, Today, milesPerDay: 5);
        var fast = ReminderProjector.Project(reminder, CurrentOdometer, Today, milesPerDay: 200);

        // Assert — 1,088 miles is 218 days at 5 a day, but under a week at 200.
        Assert.That(slow.Band, Is.EqualTo(DueBand.Later));
        Assert.That(fast.Band, Is.EqualTo(DueBand.DueSoon));
    }

    [Test]
    public void TestProject_WhenThereIsNoDailyAverageYet_JudgesAMileageTriggerOnDistance()
    {
        // Arrange — a brand new vehicle has no rate to project with.
        var reminder = new Reminder(VehicleId, "Oil & filter", 5_000, null, 88_000, Today);

        // Act
        var projection = ReminderProjector.Project(reminder, CurrentOdometer, Today, milesPerDay: null);

        // Assert — 4,588 miles away is beyond the no-rate "due soon" distance.
        Assert.That(projection.Band, Is.EqualTo(DueBand.Later));
        Assert.That(projection.MilesRemaining, Is.EqualTo(4_588));
    }

    [Test]
    public void TestProject_WhenTheTimeTriggerHasPassedButMileageHasNot_IsStillOverdue()
    {
        // Arrange — an annual item on a car that barely moves.
        var reminder = new Reminder(VehicleId, "State inspection", 20_000, 12, 88_000, new DateOnly(2025, 6, 1));

        // Act
        var projection = ReminderProjector.Project(reminder, CurrentOdometer, Today, MilesPerDay);

        // Assert
        Assert.That(projection.Band, Is.EqualTo(DueBand.Overdue));
        Assert.That(projection.RemainingDescription, Does.Contain("past due"));
    }

    [Test]
    public void TestProject_WhenHalfTheMileageIntervalIsUsed_ReportsHalfProgress()
    {
        // Arrange — story S-3's progress figure, from the anchor to the trigger.
        var reminder = new Reminder(VehicleId, "Tire rotation", 6_000, null, 85_412, new DateOnly(2026, 3, 2));

        // Act
        var projection = ReminderProjector.Project(reminder, CurrentOdometer, Today, MilesPerDay);

        // Assert — 3,000 of 6,000 miles covered.
        Assert.That(projection.MileageProgress, Is.EqualTo(0.5).Within(0.001));
    }

    [Test]
    public void TestProject_WhenSnoozed_MovesTheDuePointOutOfOverdue()
    {
        // Arrange — story S-4.
        var reminder = new Reminder(VehicleId, "Oil & filter", 5_000, 6, 82_500, new DateOnly(2026, 2, 10));
        reminder.Snooze(CurrentOdometer, Today, byMiles: 500, byMonths: 1);

        // Act
        var projection = ReminderProjector.Project(reminder, CurrentOdometer, Today, MilesPerDay);

        // Assert
        Assert.That(projection.Band, Is.Not.EqualTo(DueBand.Overdue));
        Assert.That(projection.MilesRemaining, Is.EqualTo(500));
    }
}
