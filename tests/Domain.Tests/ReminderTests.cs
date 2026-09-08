using Garage.Domain.Common;
using Garage.Domain.Entities;
using NUnit.Framework;

namespace Garage.Domain.Tests;

[TestFixture]
public class ReminderTests
{
    private static readonly Guid VehicleId = Guid.NewGuid();
    private static readonly DateOnly Today = new(2026, 8, 13);
    private static readonly DateOnly LastService = new(2026, 2, 10);

    [Test]
    public void TestConstructor_WhenNeitherIntervalIsGiven_ThrowsDomainException()
    {
        // Arrange
        int? noMileage = null;
        int? noMonths = null;

        // Act & Assert
        Assert.Throws<DomainException>(() =>
            new Reminder(VehicleId, "Oil & filter", noMileage, noMonths, 82_500, LastService));
    }

    [Test]
    public void TestDueOdometer_WhenAMileageIntervalIsSet_IsTheAnchorPlusTheInterval()
    {
        // Arrange
        var reminder = new Reminder(VehicleId, "Oil & filter", 5_000, 6, 82_500, LastService);

        // Act
        var dueOdometer = reminder.DueOdometer;

        // Assert
        Assert.That(dueOdometer, Is.EqualTo(87_500));
    }

    [Test]
    public void TestDueDate_WhenAMonthIntervalIsSet_IsTheAnchorPlusTheMonths()
    {
        // Arrange
        var reminder = new Reminder(VehicleId, "Oil & filter", 5_000, 6, 82_500, LastService);

        // Act
        var dueDate = reminder.DueDate;

        // Assert
        Assert.That(dueDate, Is.EqualTo(new DateOnly(2026, 8, 10)));
    }

    [Test]
    public void TestDueOdometer_WhenOnlyAMonthIntervalIsSet_IsNull()
    {
        // Arrange
        var reminder = new Reminder(VehicleId, "Cabin air filter", null, 12, 82_100, new DateOnly(2025, 12, 15));

        // Act
        var dueOdometer = reminder.DueOdometer;

        // Assert
        Assert.That(dueOdometer, Is.Null);
    }

    [Test]
    public void TestTriggerDescription_WhenBothIntervalsAreSet_SaysWhicheverComesFirst()
    {
        // Arrange — story S-1 wants the combined rule stated in words.
        var reminder = new Reminder(VehicleId, "Oil & filter", 5_000, 6, 82_500, LastService);

        // Act
        var description = reminder.TriggerDescription;

        // Assert
        Assert.That(description, Is.EqualTo("87,500 mi or Aug 2026 — whichever first"));
    }

    [Test]
    public void TestCompleteAt_WhenTheItemRepeats_ReAnchorsToTheServiceJustLogged()
    {
        // Arrange
        var reminder = new Reminder(VehicleId, "Oil & filter", 5_000, 6, 82_500, LastService, repeatAfterService: true);

        // Act
        reminder.CompleteAt(88_412, Today);

        // Assert
        Assert.That(reminder.DueOdometer, Is.EqualTo(93_412));
        Assert.That(reminder.DueDate, Is.EqualTo(new DateOnly(2027, 2, 13)));
        Assert.That(reminder.IsDismissed, Is.False);
    }

    [Test]
    public void TestCompleteAt_WhenTheItemDoesNotRepeat_RetiresIt()
    {
        // Arrange
        var reminder = new Reminder(VehicleId, "Timing belt", 105_000, null, 15_000, LastService, repeatAfterService: false);

        // Act
        reminder.CompleteAt(120_000, Today);

        // Assert
        Assert.That(reminder.IsDismissed, Is.True);
    }

    [Test]
    public void TestSnooze_WhenDeferredByMiles_PushesTheDueOdometerPastTheCurrentReading()
    {
        // Arrange — the item is already 912 miles overdue at 88,412.
        var reminder = new Reminder(VehicleId, "Oil & filter", 5_000, 6, 82_500, LastService);

        // Act
        reminder.Snooze(88_412, Today, byMiles: 500, byMonths: null);

        // Assert
        Assert.That(reminder.DueOdometer, Is.EqualTo(88_912));
    }

    [Test]
    public void TestSnooze_WhenDeferredByMonthsOnAnOverdueItem_CountsFromToday()
    {
        // Arrange
        var reminder = new Reminder(VehicleId, "Oil & filter", 5_000, 6, 82_500, LastService);

        // Act
        reminder.Snooze(88_412, Today, byMiles: null, byMonths: 1);

        // Assert
        Assert.That(reminder.DueDate, Is.EqualTo(new DateOnly(2026, 9, 13)));
    }

    [Test]
    public void TestSnooze_WhenNoDeferralIsChosen_ThrowsDomainException()
    {
        // Arrange
        var reminder = new Reminder(VehicleId, "Oil & filter", 5_000, 6, 82_500, LastService);

        // Act & Assert
        Assert.Throws<DomainException>(() => reminder.Snooze(88_412, Today, byMiles: null, byMonths: null));
    }
}
