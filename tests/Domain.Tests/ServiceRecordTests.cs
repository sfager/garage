using Garage.Domain;
using Garage.Domain.Common;
using Garage.Domain.Entities;
using NUnit.Framework;

namespace Garage.Domain.Tests;

[TestFixture]
public class ServiceRecordTests
{
    private static readonly Guid VehicleId = Guid.NewGuid();
    private static readonly DateOnly Today = new(2026, 8, 13);

    private ServiceRecord _record = default!;

    [SetUp]
    public void Setup()
    {
        _record = new ServiceRecord(VehicleId, Today, 88_412, ServiceCategory.ScheduledService, 74.20m);
    }

    [Test]
    public void TestSetCostBreakdown_WhenPartsAndLaborFitInsideTheTotal_StoresBothValues()
    {
        // Arrange
        var parts = 32.00m;
        var labor = 42.20m;

        // Act
        _record.SetCostBreakdown(parts, labor);

        // Assert
        Assert.That(_record.PartsCost, Is.EqualTo(parts));
        Assert.That(_record.LaborCost, Is.EqualTo(labor));
    }

    [Test]
    public void TestSetCostBreakdown_WhenPartsAndLaborExceedTheTotal_ThrowsDomainException()
    {
        // Arrange — story L-2: the split may not add up to more than the total.
        var parts = 60.00m;
        var labor = 40.00m;

        // Act & Assert
        Assert.Throws<DomainException>(() => _record.SetCostBreakdown(parts, labor));
    }

    [Test]
    public void TestSetTotalCost_WhenLoweredBelowAnExistingBreakdown_ThrowsDomainException()
    {
        // Arrange
        _record.SetCostBreakdown(32.00m, 42.20m);

        // Act & Assert
        Assert.Throws<DomainException>(() => _record.SetTotalCost(50.00m));
    }

    [Test]
    public void TestSummary_WhenTheVisitCoversSeveralJobs_NamesTheFirstAndCountsTheRest()
    {
        // Arrange
        _record.AddItem("Oil & filter");
        _record.AddItem("Tire rotation");
        _record.AddItem("Wipers");

        // Act
        var summary = _record.Summary;

        // Assert
        Assert.That(summary, Is.EqualTo("Oil & filter + 2 more"));
    }

    [Test]
    public void TestSummary_WhenTheVisitCoversOneJob_NamesThatJob()
    {
        // Arrange
        _record.AddItem("Oil & filter");

        // Act
        var summary = _record.Summary;

        // Assert
        Assert.That(summary, Is.EqualTo("Oil & filter"));
    }

    [Test]
    public void TestAddItem_WhenTheNameIsBlank_ThrowsDomainException()
    {
        // Arrange
        var blankName = "  ";

        // Act & Assert
        Assert.Throws<DomainException>(() => _record.AddItem(blankName));
    }
}
