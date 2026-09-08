using Garage.Application.Abstractions;
using Garage.Application.Notifications;
using Garage.Domain;
using Garage.Domain.Common;
using Garage.Domain.Entities;
using Garage.Domain.Repositories;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;
using DomainPushSubscription = Garage.Domain.Entities.PushSubscription;

namespace Garage.Domain.Tests;

[TestFixture]
public class NotificationServiceTests
{
    private static readonly Guid HouseholdId = Guid.NewGuid();
    private static readonly Guid VehicleId = Guid.NewGuid();
    private static readonly DateOnly Today = new(2026, 8, 16);

    private FakeScan _scan = default!;
    private FakeSubscriptions _subscriptions = default!;
    private FakeSentNotifications _sent = default!;
    private RecordingPushSender _sender = default!;
    private NotificationService _service = default!;

    [SetUp]
    public void Setup()
    {
        _scan = new FakeScan();
        _subscriptions = new FakeSubscriptions();
        _sent = new FakeSentNotifications();
        _sender = new RecordingPushSender();

        _subscriptions.Items.Add(new DomainPushSubscription(HouseholdId, "user-1", "https://push.example/abc", "key", "auth"));
        _scan.Households.Add(HouseholdId);

        _service = new NotificationService(
            _scan, _subscriptions, _sent, new FakeMileage(),
            _sender, new FakeUnitOfWork(), new FakeClock(Today), NullLogger<NotificationService>.Instance);
    }

    /// <summary>Overdue by 912 miles, exactly like the seeded oil change.</summary>
    private static Reminder OverdueReminder() =>
        new(VehicleId, "Oil & filter", 5_000, 6, 82_500, new DateOnly(2026, 2, 10));

    [Test]
    public async Task TestSweepAsync_WhenAReminderIsOverdue_SendsOneNotification()
    {
        // Arrange
        _scan.Reminders.Add((OverdueReminder(), "Outback", 88_412));

        // Act
        var result = await _service.SweepAsync();

        // Assert
        Assert.That(result.Sent, Is.EqualTo(1));
        Assert.That(_sender.Sent.Single().Title, Is.EqualTo("Outback: Oil & filter"));
        Assert.That(_sender.Sent.Single().Body, Does.Contain("912 mi past due"));
    }

    [Test]
    public async Task TestSweepAsync_WhenTheReminderIsNotDueYet_SaysNothing()
    {
        // Arrange — due at 93,500 with the car at 88,412.
        _scan.Reminders.Add((new Reminder(VehicleId, "Tire rotation", 5_000, 12, 88_500, Today), "Outback", 88_412));

        // Act
        var result = await _service.SweepAsync();

        // Assert
        Assert.That(result.Sent, Is.Zero);
        Assert.That(_sender.Sent, Is.Empty);
    }

    [Test]
    public async Task TestSweepAsync_WhenNotificationsAreOffForThatReminder_SkipsIt()
    {
        // Arrange — story S-5's per-reminder switch.
        var reminder = OverdueReminder();
        reminder.SetNotifications(false);
        _scan.Reminders.Add((reminder, "Outback", 88_412));

        // Act
        var result = await _service.SweepAsync();

        // Assert
        Assert.That(result.Sent, Is.Zero);
    }

    [Test]
    public async Task TestSweepAsync_WhenRunTwice_DoesNotRepeatItself()
    {
        // Arrange — a due point stays due for weeks; it should be announced once.
        _scan.Reminders.Add((OverdueReminder(), "Outback", 88_412));

        // Act
        await _service.SweepAsync();
        var second = await _service.SweepAsync();

        // Assert
        Assert.That(second.Sent, Is.Zero);
        Assert.That(second.Skipped, Is.EqualTo(1));
        Assert.That(_sender.Sent, Has.Count.EqualTo(1));
    }

    [Test]
    public async Task TestSweepAsync_WhenTheItemComesDueAtANewPoint_SpeaksUpAgain()
    {
        // Arrange — the oil is changed, which re-anchors the reminder to 93,412, and the
        // car later passes that. A different due point is a different thing to say.
        var reminder = OverdueReminder();
        _scan.Reminders.Add((reminder, "Outback", 88_412));
        await _service.SweepAsync();

        reminder.CompleteAt(88_412, Today);
        _scan.Reminders.Clear();
        _scan.Reminders.Add((reminder, "Outback", 94_000));

        // Act
        var result = await _service.SweepAsync();

        // Assert
        Assert.That(result.Sent, Is.EqualTo(1));
        Assert.That(_sender.Sent, Has.Count.EqualTo(2));
        Assert.That(_sent.Items.Select(n => n.SubjectKey).Distinct().Count(), Is.EqualTo(2));
    }

    [Test]
    public async Task TestSweepAsync_WhenSnoozed_GoesQuietUntilTheNewPointArrives()
    {
        // Arrange — story S-4: snoozing is a request not to be told for a while.
        var reminder = OverdueReminder();
        _scan.Reminders.Add((reminder, "Outback", 88_412));
        await _service.SweepAsync();

        reminder.Snooze(88_412, Today, byMiles: 500, byMonths: null);
        _sender.Sent.Clear();

        // Act
        var result = await _service.SweepAsync();

        // Assert
        Assert.That(result.Sent, Is.Zero);
        Assert.That(_sender.Sent, Is.Empty);
    }

    [Test]
    public async Task TestSweepAsync_WhenDeliveryFails_DoesNotRecordItAsTold()
    {
        // Arrange — a transient outage must not swallow the notification for good.
        _scan.Reminders.Add((OverdueReminder(), "Outback", 88_412));
        _sender.Result = PushResult.Failed;

        // Act
        var first = await _service.SweepAsync();
        _sender.Result = PushResult.Sent;
        var second = await _service.SweepAsync();

        // Assert
        Assert.That(first.Sent, Is.Zero);
        Assert.That(second.Sent, Is.EqualTo(1));
    }

    [Test]
    public async Task TestSweepAsync_WhenTheSubscriptionHasExpired_DropsIt()
    {
        // Arrange — the browser was reinstalled or the permission revoked.
        _scan.Reminders.Add((OverdueReminder(), "Outback", 88_412));
        _sender.Result = PushResult.SubscriptionExpired;

        // Act
        var result = await _service.SweepAsync();

        // Assert
        Assert.That(result.SubscriptionsDropped, Is.EqualTo(1));
        Assert.That(_subscriptions.Items, Is.Empty);
    }

    [Test]
    public async Task TestSweepAsync_WhenADocumentIsAboutToExpire_MentionsIt()
    {
        // Arrange — story D-2 feeding S-5.
        var document = new Document(VehicleId, DocumentType.Registration, "Registration",
            "reg.png", "image/png", "documents/reg.png", 1024);
        document.SetExpiry(Today.AddDays(18));
        _scan.Documents.Add((document, "Outback"));

        // Act
        var result = await _service.SweepAsync();

        // Assert
        Assert.That(result.Sent, Is.EqualTo(1));
        Assert.That(_sender.Sent.Single().Title, Is.EqualTo("Outback: Registration"));
        Assert.That(_sender.Sent.Single().Body, Is.EqualTo("Expires in 18 days"));
    }

    [Test]
    public async Task TestSweepAsync_WhenPushIsNotConfigured_StandsDownQuietly()
    {
        // Arrange — no VAPID keys on the server.
        _scan.Reminders.Add((OverdueReminder(), "Outback", 88_412));
        _sender.Configured = false;

        // Act
        var result = await _service.SweepAsync();

        // Assert
        Assert.That(result.HouseholdsScanned, Is.Zero);
        Assert.That(_sender.Sent, Is.Empty);
    }

    [Test]
    public async Task TestSweepAsync_WhenNobodyHasSubscribed_SendsNothing()
    {
        // Arrange
        _scan.Reminders.Add((OverdueReminder(), "Outback", 88_412));
        _subscriptions.Items.Clear();

        // Act
        var result = await _service.SweepAsync();

        // Assert
        Assert.That(result.Sent, Is.Zero);
        Assert.That(_sent.Items, Is.Empty);
    }

    // ---- test doubles -------------------------------------------------------

    private sealed class FakeScan : INotificationScanRepository
    {
        public List<Guid> Households { get; } = [];
        public List<(Reminder, string, int)> Reminders { get; } = [];
        public List<(Document, string)> Documents { get; } = [];

        public Task<IReadOnlyList<Guid>> ListHouseholdIdsWithSubscriptionsAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Guid>>(Households);

        public Task<IReadOnlyList<(Reminder Reminder, string VehicleNickname, int CurrentOdometer)>> ListActiveRemindersAsync(
            Guid householdId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<(Reminder, string, int)>>(Reminders);

        public Task<IReadOnlyList<(Document Document, string VehicleNickname)>> ListExpiringDocumentsAsync(
            Guid householdId, DateOnly through, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<(Document, string)>>(
                [.. Documents.Where(d => d.Item1.ExpiresOn <= through)]);
    }

    private sealed class FakeSubscriptions : IPushSubscriptionRepository
    {
        public List<DomainPushSubscription> Items { get; } = [];

        public Task<DomainPushSubscription?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult(Items.FirstOrDefault(s => s.Id == id));

        public Task AddAsync(DomainPushSubscription entity, CancellationToken cancellationToken = default)
        {
            Items.Add(entity);
            return Task.CompletedTask;
        }

        public void Remove(DomainPushSubscription entity) => Items.Remove(entity);

        public Task<IReadOnlyList<DomainPushSubscription>> ListForHouseholdAsync(Guid householdId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<DomainPushSubscription>>([.. Items.Where(s => s.HouseholdId == householdId)]);

        public Task<IReadOnlyList<DomainPushSubscription>> ListAllAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<DomainPushSubscription>>(Items);

        public Task<DomainPushSubscription?> GetByEndpointAsync(string endpoint, CancellationToken cancellationToken = default) =>
            Task.FromResult(Items.FirstOrDefault(s => s.Endpoint == endpoint));
    }

    private sealed class FakeSentNotifications : ISentNotificationRepository
    {
        public List<SentNotification> Items { get; } = [];

        public Task<SentNotification?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult(Items.FirstOrDefault(n => n.Id == id));

        public Task AddAsync(SentNotification entity, CancellationToken cancellationToken = default)
        {
            Items.Add(entity);
            return Task.CompletedTask;
        }

        public void Remove(SentNotification entity) => Items.Remove(entity);

        public Task<IReadOnlySet<string>> ListSentKeysAsync(Guid householdId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlySet<string>>(
                Items.Where(n => n.HouseholdId == householdId).Select(n => n.SubjectKey).ToHashSet(StringComparer.Ordinal));

        public Task PruneAsync(DateTimeOffset before, CancellationToken cancellationToken = default)
        {
            Items.RemoveAll(n => n.SentUtc < before);
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingPushSender : IPushSender
    {
        public List<PushMessage> Sent { get; } = [];
        public PushResult Result { get; set; } = PushResult.Sent;
        public bool Configured { get; set; } = true;

        public string PublicKey => "test-key";
        public bool IsConfigured => Configured;

        public Task<PushResult> SendAsync(DomainPushSubscription subscription, PushMessage message, CancellationToken cancellationToken = default)
        {
            if (Result == PushResult.Sent)
            {
                Sent.Add(message);
            }

            return Task.FromResult(Result);
        }
    }

    private sealed class FakeMileage : IMileageRepository
    {
        public Task<IReadOnlyList<OdometerReading>> ListReadingsAsync(Guid vehicleId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<OdometerReading>>([]);

        public Task<IReadOnlyList<Trip>> ListTripsAsync(Guid vehicleId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Trip>>([]);

        public Task<IReadOnlyList<(DateOnly Date, int Odometer, bool IsReading)>> ListPointsAsync(Guid vehicleId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<(DateOnly, int, bool)>>(
            [
                (new DateOnly(2026, 6, 1), 86_000, true),
                (Today, 88_412, true)
            ]);
    }

    private sealed class FakeUnitOfWork : IUnitOfWork
    {
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => Task.FromResult(0);
    }

    private sealed class FakeClock(DateOnly today) : IClock
    {
        public DateTimeOffset UtcNow => new(today.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
        public DateOnly Today => today;
    }
}
