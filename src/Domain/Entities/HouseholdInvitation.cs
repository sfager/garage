using System.Security.Cryptography;
using System.Text;
using Garage.Domain.Common;

namespace Garage.Domain.Entities;

/// <summary>
/// An invitation to join a household, so two people can keep the same cars.
///
/// Only a hash of the code is stored. The code itself is shown once, when it is created —
/// a database that leaks should not hand anybody the keys to somebody's garage.
/// </summary>
public class HouseholdInvitation : Entity
{
    /// <summary>Long enough to be unguessable, short enough to read down a phone.</summary>
    private const int CodeBytes = 20;

    /// <summary>An unused invitation stops working after a week.</summary>
    public static readonly TimeSpan Lifetime = TimeSpan.FromDays(7);

    private HouseholdInvitation() { }

    private HouseholdInvitation(Guid householdId, string createdByUserId, string code, DateTimeOffset now)
    {
        if (householdId == Guid.Empty)
        {
            throw new DomainException("An invitation must belong to a household.");
        }

        HouseholdId = householdId;
        CreatedByUserId = createdByUserId;
        CodeHash = HashCode(code);
        CreatedUtc = now;
        ExpiresUtc = now + Lifetime;
    }

    public Guid HouseholdId { get; private set; }
    public string CodeHash { get; private set; } = string.Empty;
    public string CreatedByUserId { get; private set; } = string.Empty;

    public DateTimeOffset CreatedUtc { get; private set; }
    public DateTimeOffset ExpiresUtc { get; private set; }

    public DateTimeOffset? AcceptedUtc { get; private set; }
    public string? AcceptedByUserId { get; private set; }
    public DateTimeOffset? RevokedUtc { get; private set; }

    /// <summary>Still usable: not taken, not withdrawn, not out of date.</summary>
    public bool IsPending(DateTimeOffset now) =>
        AcceptedUtc is null && RevokedUtc is null && now < ExpiresUtc;

    public InvitationProblem? ProblemFor(DateTimeOffset now) => this switch
    {
        { AcceptedUtc: not null } => InvitationProblem.AlreadyUsed,
        { RevokedUtc: not null } => InvitationProblem.Withdrawn,
        _ when now >= ExpiresUtc => InvitationProblem.Expired,
        _ => null
    };

    /// <summary>
    /// Creates an invitation and hands back the one and only copy of its code.
    /// </summary>
    public static (HouseholdInvitation Invitation, string Code) Create(
        Guid householdId,
        string createdByUserId,
        DateTimeOffset now)
    {
        var code = GenerateCode();
        return (new HouseholdInvitation(householdId, createdByUserId, code, now), code);
    }

    /// <summary>Single use: an accepted invitation cannot let a third person in.</summary>
    public void Accept(string userId, DateTimeOffset now)
    {
        if (ProblemFor(now) is { } problem)
        {
            throw new DomainException(problem switch
            {
                InvitationProblem.AlreadyUsed => "That invitation has already been used.",
                InvitationProblem.Withdrawn => "That invitation was withdrawn.",
                _ => "That invitation has expired. Ask for a new one."
            });
        }

        AcceptedUtc = now;
        AcceptedByUserId = userId;
    }

    public void Revoke(DateTimeOffset now)
    {
        if (AcceptedUtc is not null)
        {
            throw new DomainException("That invitation has already been used.");
        }

        RevokedUtc = now;
    }

    /// <summary>
    /// Base32 over an unambiguous alphabet — no I, O, 0 or 1 — so a code read aloud or
    /// copied off a screen does not turn into a different code.
    /// </summary>
    private static string GenerateCode()
    {
        const string alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        var bytes = RandomNumberGenerator.GetBytes(CodeBytes);
        var builder = new StringBuilder(CodeBytes + CodeBytes / 4);

        for (var i = 0; i < bytes.Length; i++)
        {
            if (i > 0 && i % 4 == 0)
            {
                builder.Append('-');
            }

            builder.Append(alphabet[bytes[i] % alphabet.Length]);
        }

        return builder.ToString();
    }

    /// <summary>Normalises before hashing so case and stray dashes do not matter.</summary>
    public static string HashCode(string code)
    {
        var normalised = Normalize(code);
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(normalised));
        return Convert.ToHexString(hash);
    }

    public static string Normalize(string code) =>
        new([.. (code ?? string.Empty).ToUpperInvariant().Where(char.IsAsciiLetterOrDigit)]);
}

public enum InvitationProblem
{
    AlreadyUsed = 0,
    Withdrawn = 1,
    Expired = 2
}
