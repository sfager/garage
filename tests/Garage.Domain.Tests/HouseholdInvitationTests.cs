using Garage.Domain.Common;
using Garage.Domain.Entities;
using NUnit.Framework;

namespace Garage.Domain.Tests;

[TestFixture]
public class HouseholdInvitationTests
{
    private static readonly Guid HouseholdId = Guid.NewGuid();
    private static readonly DateTimeOffset Now = new(2026, 8, 16, 12, 0, 0, TimeSpan.Zero);

    private const string Inviter = "user-1";
    private const string Joiner = "user-2";

    [Test]
    public void TestCreate_WhenCreated_StoresOnlyAHashOfTheCode()
    {
        // Arrange
        // (an invitation for a household)

        // Act
        var (invitation, code) = HouseholdInvitation.Create(HouseholdId, Inviter, Now);

        // Assert — a leaked database must not hand anyone the keys to a garage.
        Assert.That(invitation.CodeHash, Is.Not.EqualTo(code));
        Assert.That(invitation.CodeHash, Does.Not.Contain(code.Replace("-", string.Empty)));
        Assert.That(invitation.CodeHash, Has.Length.EqualTo(64));
    }

    [Test]
    public void TestCreate_WhenCreated_ProducesACodeWithoutAmbiguousCharacters()
    {
        // Arrange — I, O, 0 and 1 are excluded so a code read aloud stays the same code.
        var (_, code) = HouseholdInvitation.Create(HouseholdId, Inviter, Now);

        // Act
        var characters = code.Replace("-", string.Empty);

        // Assert
        Assert.That(characters, Does.Not.Contain("I"));
        Assert.That(characters, Does.Not.Contain("O"));
        Assert.That(characters, Does.Not.Contain("0"));
        Assert.That(characters, Does.Not.Contain("1"));
    }

    [Test]
    public void TestCreate_WhenCalledRepeatedly_ProducesDifferentCodes()
    {
        // Arrange
        var codes = new HashSet<string>();

        // Act
        for (var i = 0; i < 50; i++)
        {
            codes.Add(HouseholdInvitation.Create(HouseholdId, Inviter, Now).Code);
        }

        // Assert
        Assert.That(codes, Has.Count.EqualTo(50));
    }

    [TestCase("abcd-efgh")]
    [TestCase("ABCDEFGH")]
    [TestCase("  abcd efgh  ")]
    public void TestHashCode_WhateverTheFormatting_ProducesTheSameHash(string variant)
    {
        // Arrange — a code typed with different case or spacing is the same code.
        var canonical = HouseholdInvitation.HashCode("ABCD-EFGH");

        // Act
        var hash = HouseholdInvitation.HashCode(variant);

        // Assert
        Assert.That(hash, Is.EqualTo(canonical));
    }

    [Test]
    public void TestAccept_WhenPending_MarksItUsedByThatPerson()
    {
        // Arrange
        var (invitation, _) = HouseholdInvitation.Create(HouseholdId, Inviter, Now);

        // Act
        invitation.Accept(Joiner, Now.AddHours(1));

        // Assert
        Assert.That(invitation.AcceptedByUserId, Is.EqualTo(Joiner));
        Assert.That(invitation.IsPending(Now.AddHours(2)), Is.False);
    }

    [Test]
    public void TestAccept_WhenAlreadyUsed_RefusesASecondPerson()
    {
        // Arrange — an invitation is single use.
        var (invitation, _) = HouseholdInvitation.Create(HouseholdId, Inviter, Now);
        invitation.Accept(Joiner, Now.AddHours(1));

        // Act & Assert
        var ex = Assert.Throws<DomainException>(() => invitation.Accept("user-3", Now.AddHours(2)));
        Assert.That(ex!.Message, Does.Contain("already been used"));
    }

    [Test]
    public void TestAccept_WhenExpired_RefusesAndSaysSo()
    {
        // Arrange
        var (invitation, _) = HouseholdInvitation.Create(HouseholdId, Inviter, Now);

        // Act & Assert
        var ex = Assert.Throws<DomainException>(() =>
            invitation.Accept(Joiner, Now + HouseholdInvitation.Lifetime + TimeSpan.FromMinutes(1)));
        Assert.That(ex!.Message, Does.Contain("expired"));
    }

    [Test]
    public void TestAccept_WhenWithdrawn_Refuses()
    {
        // Arrange
        var (invitation, _) = HouseholdInvitation.Create(HouseholdId, Inviter, Now);
        invitation.Revoke(Now.AddMinutes(5));

        // Act & Assert
        var ex = Assert.Throws<DomainException>(() => invitation.Accept(Joiner, Now.AddHours(1)));
        Assert.That(ex!.Message, Does.Contain("withdrawn"));
    }

    [Test]
    public void TestRevoke_WhenAlreadyAccepted_Refuses()
    {
        // Arrange — withdrawing after the fact would not undo the join.
        var (invitation, _) = HouseholdInvitation.Create(HouseholdId, Inviter, Now);
        invitation.Accept(Joiner, Now.AddHours(1));

        // Act & Assert
        Assert.Throws<DomainException>(() => invitation.Revoke(Now.AddHours(2)));
    }

    [Test]
    public void TestIsPending_WhileWithinItsLifetime_IsTrue()
    {
        // Arrange
        var (invitation, _) = HouseholdInvitation.Create(HouseholdId, Inviter, Now);

        // Act & Assert
        Assert.That(invitation.IsPending(Now.AddDays(6)), Is.True);
        Assert.That(invitation.IsPending(Now.AddDays(8)), Is.False);
    }

    [Test]
    public void TestCreate_WithoutAHousehold_ThrowsDomainException()
    {
        // Arrange
        var missing = Guid.Empty;

        // Act & Assert
        Assert.Throws<DomainException>(() => HouseholdInvitation.Create(missing, Inviter, Now));
    }

    [Test]
    public void TestMoveToHousehold_WhenJoiningAnotherGarage_TakesTheVehicleAlong()
    {
        // Arrange — the joiner's cars come with them rather than being stranded.
        var vehicle = new Vehicle(HouseholdId, "Vespa", 4_200, new DateOnly(2026, 8, 16));
        var destination = Guid.NewGuid();

        // Act
        vehicle.MoveToHousehold(destination);

        // Assert
        Assert.That(vehicle.HouseholdId, Is.EqualTo(destination));
    }

    [Test]
    public void TestMoveToHousehold_WithoutAHousehold_ThrowsDomainException()
    {
        // Arrange
        var vehicle = new Vehicle(HouseholdId, "Vespa", 4_200, new DateOnly(2026, 8, 16));

        // Act & Assert
        Assert.Throws<DomainException>(() => vehicle.MoveToHousehold(Guid.Empty));
    }
}
