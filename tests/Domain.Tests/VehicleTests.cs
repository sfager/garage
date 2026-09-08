using Garage.Domain;
using Garage.Domain.Common;
using Garage.Domain.Entities;
using NUnit.Framework;

namespace Garage.Domain.Tests;

[TestFixture]
public class VehicleTests
{
    private static readonly Guid HouseholdId = Guid.NewGuid();
    private static readonly DateOnly Today = new(2026, 8, 13);

    private Vehicle _vehicle = default!;

    [SetUp]
    public void Setup()
    {
        _vehicle = new Vehicle(HouseholdId, "Outback", 88_000, new DateOnly(2026, 8, 1));
    }

    [Test]
    public void TestConstructor_WhenCreated_RecordsTheStartingOdometerAsAReading()
    {
        // Arrange
        // (the SUT is built in Setup with a starting odometer of 88,000)

        // Act
        var readings = _vehicle.OdometerReadings;

        // Assert
        Assert.That(readings, Has.Count.EqualTo(1));
        Assert.That(readings.Single().Odometer, Is.EqualTo(88_000));
        Assert.That(readings.Single().Source, Is.EqualTo(OdometerSource.VehicleSetup));
    }

    [Test]
    public void TestConstructor_WhenHouseholdIsMissing_ThrowsDomainException()
    {
        // Arrange
        var noHousehold = Guid.Empty;

        // Act & Assert
        Assert.Throws<DomainException>(() => new Vehicle(noHousehold, "Outback", 10, Today));
    }

    [Test]
    public void TestRecordReading_WhenReadingMovesForward_UpdatesCurrentOdometer()
    {
        // Arrange
        var newOdometer = 88_412;

        // Act
        _vehicle.RecordReading(Today, newOdometer);

        // Assert
        Assert.That(_vehicle.CurrentOdometer, Is.EqualTo(newOdometer));
        Assert.That(_vehicle.CurrentOdometerDate, Is.EqualTo(Today));
    }

    [Test]
    public void TestRecordReading_WhenReadingIsBelowTheLastOne_ThrowsDomainException()
    {
        // Arrange — story M-1: a lower reading is rejected with a message naming both values.
        var tooLow = 87_500;

        // Act & Assert
        var exception = Assert.Throws<DomainException>(() => _vehicle.RecordReading(Today, tooLow));
        Assert.That(exception!.Message, Does.Contain("87,500").And.Contain("88,000"));
    }

    [Test]
    public void TestRecordTrip_WhenTripCompletes_AdvancesTheOdometerByTheDistance()
    {
        // Arrange
        var trip = Trip.FromDistance(_vehicle.Id, Today, 88_000, 148, "Home → Portland", TripPurpose.Business);

        // Act
        _vehicle.RecordTrip(trip);

        // Assert
        Assert.That(_vehicle.CurrentOdometer, Is.EqualTo(88_148));
    }

    [Test]
    public void TestRecordService_WhenBackDatedBelowCurrentOdometer_LeavesTheOdometerAlone()
    {
        // Arrange — a service entered late must not drag the running total backwards.
        var backDated = new ServiceRecord(_vehicle.Id, new DateOnly(2026, 2, 10), 82_500,
            ServiceCategory.ScheduledService, 68.00m);

        // Act
        _vehicle.RecordService(backDated);

        // Assert
        Assert.That(_vehicle.CurrentOdometer, Is.EqualTo(88_000));
    }

    [Test]
    public void TestDisplayName_WhenDetailsAreSet_ReadsAsYearMakeModelTrim()
    {
        // Arrange
        _vehicle.SetDetails(2019, "Subaru", "Outback", "2.5i Premium", "2.5L H4", "4S4BSANC1K3311204", "xkd4417");

        // Act
        var displayName = _vehicle.DisplayName;

        // Assert
        Assert.That(displayName, Is.EqualTo("2019 Subaru Outback 2.5i Premium"));
    }

    [Test]
    public void TestDisplayName_WhenNoDetailsAreSet_FallsBackToTheNickname()
    {
        // Arrange
        // (Setup leaves year, make and model empty)

        // Act
        var displayName = _vehicle.DisplayName;

        // Assert
        Assert.That(displayName, Is.EqualTo("Outback"));
    }
}
