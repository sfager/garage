using Garage.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Garage.Infrastructure.Persistence.Configurations;

public class PushSubscriptionConfiguration : IEntityTypeConfiguration<PushSubscription>
{
    public void Configure(EntityTypeBuilder<PushSubscription> builder)
    {
        builder.ToTable("PushSubscriptions");
        builder.HasKey(s => s.Id);

        // Push endpoints run long; SQL Server cannot index the full length, so the
        // uniqueness that matters is enforced by looking the endpoint up before insert.
        builder.Property(s => s.Endpoint).HasMaxLength(900).IsRequired();
        builder.Property(s => s.P256dh).HasMaxLength(200).IsRequired();
        builder.Property(s => s.Auth).HasMaxLength(100).IsRequired();
        builder.Property(s => s.UserId).HasMaxLength(450).IsRequired();

        builder.HasIndex(s => s.Endpoint).IsUnique();
        builder.HasIndex(s => s.HouseholdId);
    }
}

public class SentNotificationConfiguration : IEntityTypeConfiguration<SentNotification>
{
    public void Configure(EntityTypeBuilder<SentNotification> builder)
    {
        builder.ToTable("SentNotifications");
        builder.HasKey(n => n.Id);

        builder.Property(n => n.SubjectKey).HasMaxLength(200).IsRequired();
        builder.Property(n => n.Title).HasMaxLength(200).IsRequired();

        // The sweep asks "what have we already said to this household?" on every pass.
        builder.HasIndex(n => new { n.HouseholdId, n.SubjectKey }).IsUnique();
        builder.HasIndex(n => n.SentUtc);
    }
}
