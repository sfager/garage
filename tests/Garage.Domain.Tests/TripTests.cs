using Garage.Domain;
using Garage.Domain.Common;
using Garage.Domain.Entities;
using NUnit.Framework;

namespace Garage.Domain.Tests;

[TestFixture]
public class TripTests
{
    private static readonly Guid VehicleId = Guid.NewGuid();
    private static readonly DateOnly Today = new(2026, 8, 11);

    [Test]
    public void TestFromDistance_WhenGivenADistance_DerivesTheEndOdometer()
    {
        // Arrange
        var startOdometer = 88_290;
        var distance = 114;

        // Act
        var trip = Trip.FromDistance(VehicleId, Today, startOdometer, distance, "Home → Portland", TripPurpose.Business);

        // Assert
        Assert.That(trip.EndOdometer, Is.EqualTo(88_404));
        Assert.That(trip.Distance, Is.EqualTo(distance));
    }

    [Test]
    public void TestFromOdometers_WhenGivenAStartAndEnd_DerivesTheDistance()
    {
        // Arrange
        var startOdometer = 88_000;
        var endOdometer = 88_126;

        // Act
        var trip = Trip.FromOdometers(VehicleId, Today, startOdometer, endOdometer, "Commute week", TripPurpose.Personal);

        // Assert
        Assert.That(trip.Distance, Is.EqualTo(126));
    }

    [TestCase(88_000, 88_000)]
    [TestCase(88_000, 87_900)]
    public void TestFromOdometers_WhenTheEndIsNotAheadOfTheStart_ThrowsDomainException(int start, int end)
    {
        // Arrange
        // (the odometer pair comes from the test case)

        // Act & Assert
        Assert.Throws<DomainException>(() =>
            Trip.FromOdometers(VehicleId, Today, start, end, "Nowhere", TripPurpose.Personal));
    }

    [Test]
    public void TestFromDistance_WhenTheLabelIsBlank_ThrowsDomainException()
    {
        // Arrange
        var blankLabel = "   ";

        // Act & Assert
        Assert.Throws<DomainException>(() =>
            Trip.FromDistance(VehicleId, Today, 88_000, 20, blankLabel, TripPurpose.Personal));
    }
}
