using Garage.Domain;
using Garage.Domain.Entities;
using Garage.Domain.Services;
using NUnit.Framework;

namespace Garage.Domain.Tests;

/// <summary>
/// Story D-2 introduced a reminder that fires on a given day rather than after an
/// interval — a registration expiring, an inspection due.
/// </summary>
[TestFixture]
public class FixedDateReminderTests
{
    private static readonly Guid VehicleId = Guid.NewGuid();
    private static readonly DateOnly Today = new(2026, 8, 16);

    [Test]
    public void TestOnDate_WhenCreated_IsDueOnThatDayWithNoMileageTrigger()
    {
        // Arrange
        var dueOn = new DateOnly(2026, 8, 27);

        // Act
        var reminder = Reminder.OnDate(VehicleId, "Registration expires", dueOn, 88_412, Today);

        // Assert
        Assert.That(reminder.DueDate, Is.EqualTo(dueOn));
        Assert.That(reminder.DueOdometer, Is.Null);
        Assert.That(reminder.RepeatAfterService, Is.False);
    }

    [Test]
    public void TestOnDate_WhenDescribed_NamesTheDayRatherThanAnInterval()
    {
        // Arrange
        var reminder = Reminder.OnDate(VehicleId, "Registration expires", new DateOnly(2026, 8, 27), 88_412, Today);

        // Act
        var trigger = reminder.TriggerDescription;

        // Assert
        Assert.That(trigger, Is.EqualTo("Aug 27, 2026"));
        Assert.That(reminder.IntervalDescription, Is.EqualTo("one-off"));
    }

    [Test]
    public void TestProject_WhenTheDayIsClose_BandsAsDueSoonAndLeadsOnTime()
    {
        // Arrange
        var reminder = Reminder.OnDate(VehicleId, "Registration expires", new DateOnly(2026, 8, 27), 88_412, Today);

        // Act
        var projection = ReminderProjector.Project(reminder, 88_412, Today, milesPerDay: 27);

        // Assert
        Assert.That(projection.Band, Is.EqualTo(DueBand.DueSoon));
        Assert.That(projection.LeadingTrigger, Is.EqualTo(DueTrigger.Time));
        Assert.That(projection.RemainingDescription, Is.EqualTo("11 days to go"));
        Assert.That(projection.MileageProgress, Is.Null);
    }

    [Test]
    public void TestProject_WhenTheDayHasPassed_BandsAsOverdue()
    {
        // Arrange
        var reminder = Reminder.OnDate(VehicleId, "Inspection expires", new DateOnly(2026, 8, 1), 88_412, Today);

        // Act
        var projection = ReminderProjector.Project(reminder, 88_412, Today, milesPerDay: 27);

        // Assert
        Assert.That(projection.Band, Is.EqualTo(DueBand.Overdue));
        Assert.That(projection.RemainingDescription, Does.Contain("past due"));
    }

    [Test]
    public void TestCompleteAt_WhenTheOneOffIsDone_RetiresItRatherThanRescheduling()
    {
        // Arrange — a registration renewal does not repeat on a mileage interval.
        var reminder = Reminder.OnDate(VehicleId, "Registration expires", new DateOnly(2026, 8, 27), 88_412, Today);

        // Act
        reminder.CompleteAt(88_500, Today);

        // Assert
        Assert.That(reminder.IsDismissed, Is.True);
    }

    [Test]
    public void TestConstructor_WhenNeitherIntervalNorDateIsGiven_StillThrows()
    {
        // Arrange
        // (no trigger of any kind)

        // Act & Assert
        Assert.Throws<Garage.Domain.Common.DomainException>(() =>
            new Reminder(VehicleId, "Nothing", null, null, 0, Today));
    }
}
