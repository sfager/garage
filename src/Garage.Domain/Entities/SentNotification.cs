using Garage.Domain.Common;

namespace Garage.Domain.Entities;

/// <summary>
/// A note that we have already told the household about a particular due point. Without
/// it, every sweep would fire the same reminder again — the trigger stays due until the
/// work is done, which can be weeks.
///
/// The key includes the due point, so a rescheduled or snoozed item becomes a new thing
/// worth mentioning while an unchanged one stays quiet.
/// </summary>
public class SentNotification : Entity
{
    private SentNotification() { }

    public SentNotification(Guid householdId, string subjectKey, string title)
    {
        if (string.IsNullOrWhiteSpace(subjectKey))
        {
            throw new DomainException("A sent notification needs a subject key.");
        }

        HouseholdId = householdId;
        SubjectKey = subjectKey;
        Title = title;
    }

    public Guid HouseholdId { get; private set; }

    /// <summary>"reminder:{id}:{dueOdometer}:{dueDate}" or "document:{id}:{expiry}".</summary>
    public string SubjectKey { get; private set; } = string.Empty;

    public string Title { get; private set; } = string.Empty;
    public DateTimeOffset SentUtc { get; private set; } = DateTimeOffset.UtcNow;
}
