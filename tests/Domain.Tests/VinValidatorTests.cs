using Garage.Domain.Services;
using NUnit.Framework;

namespace Garage.Domain.Tests;

[TestFixture]
public class VinValidatorTests
{
    /// <summary>The seeded Outback's VIN, which carries a correct check digit.</summary>
    private const string ValidVin = "4S4BSANC1K3311204";

    [Test]
    public void TestCheck_WhenTheVinIsWellFormed_PassesWithAValidCheckDigit()
    {
        // Arrange
        // (a real VIN with its ninth character correct)

        // Act
        var result = VinValidator.Check(ValidVin);

        // Assert
        Assert.That(result.IsWellFormed, Is.True);
        Assert.That(result.CheckDigitValid, Is.True);
        Assert.That(result.Problem, Is.Null);
    }

    [Test]
    public void TestCheck_WhenGivenLowercase_AcceptsItAnyway()
    {
        // Arrange
        var lowercase = ValidVin.ToLowerInvariant();

        // Act
        var result = VinValidator.Check(lowercase);

        // Assert
        Assert.That(result.CheckDigitValid, Is.True);
    }

    [TestCase("", "Enter a VIN.")]
    [TestCase("   ", "Enter a VIN.")]
    public void TestCheck_WhenNothingIsEntered_AsksForAVin(string vin, string expected)
    {
        // Arrange
        // (the input comes from the test case)

        // Act
        var result = VinValidator.Check(vin);

        // Assert
        Assert.That(result.Problem, Is.EqualTo(expected));
        Assert.That(result.CanProceed, Is.False);
    }

    [Test]
    public void TestCheck_WhenTooShort_SaysHowManyCharactersWereGiven()
    {
        // Arrange
        var short_ = "4S4BSANC1";

        // Act
        var result = VinValidator.Check(short_);

        // Assert
        Assert.That(result.Problem, Is.EqualTo("A VIN is 17 characters — that one is 9."));
    }

    [Test]
    public void TestCheck_WhenItContainsAForbiddenLetter_ExplainsWhy()
    {
        // Arrange — I, O and Q are excluded so they cannot be read as 1 and 0.
        var withO = "4S4BSANC1K33112O4";

        // Act
        var result = VinValidator.Check(withO);

        // Assert
        Assert.That(result.IsWellFormed, Is.False);
        Assert.That(result.Problem, Does.Contain("I, O or Q"));
    }

    [Test]
    public void TestCheck_WhenItContainsPunctuation_IsRejected()
    {
        // Arrange
        var withDash = "4S4BSANC1-3311204";

        // Act
        var result = VinValidator.Check(withDash);

        // Assert
        Assert.That(result.IsWellFormed, Is.False);
        Assert.That(result.Problem, Does.Contain("letters and digits"));
    }

    [Test]
    public void TestCheck_WhenACharacterIsMisread_FlagsItAsSuspiciousRatherThanRejecting()
    {
        // Arrange — the sort of single-character slip a camera makes. A European VIN can
        // legitimately fail this, so it warns instead of blocking.
        var misread = "4S4BSANC1K3311205";

        // Act
        var result = VinValidator.Check(misread);

        // Assert
        Assert.That(result.IsWellFormed, Is.True);
        Assert.That(result.CheckDigitValid, Is.False);
        Assert.That(result.IsSuspicious, Is.True);
        Assert.That(result.CanProceed, Is.True);
    }

    [Test]
    public void TestHasValidCheckDigit_WhenTheCheckDigitIsX_IsAccepted()
    {
        // Arrange — a remainder of 10 is written as X, not as a digit.
        var withX = "1M8GDM9AXKP042788";

        // Act
        var valid = VinValidator.HasValidCheckDigit(withX);

        // Assert
        Assert.That(valid, Is.True);
    }
}
