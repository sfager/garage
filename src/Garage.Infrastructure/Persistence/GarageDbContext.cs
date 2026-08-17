using Garage.Application.Abstractions;
using Garage.Domain.Common;
using Garage.Domain.Entities;
using Garage.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Garage.Infrastructure.Persistence;

/// <summary>
/// One SQL Server context for both Identity and the garage aggregates, so a user
/// and the household they belong to commit together.
/// </summary>
public class GarageDbContext(DbContextOptions<GarageDbContext> options)
    : IdentityDbContext<ApplicationUser>(options), IUnitOfWork
{
    public DbSet<Household> Households => Set<Household>();
    public DbSet<HouseholdInvitation> HouseholdInvitations => Set<HouseholdInvitation>();
    public DbSet<Vehicle> Vehicles => Set<Vehicle>();
    public DbSet<OdometerReading> OdometerReadings => Set<OdometerReading>();
    public DbSet<Trip> Trips => Set<Trip>();
    public DbSet<ServiceRecord> ServiceRecords => Set<ServiceRecord>();
    public DbSet<ServiceRecordItem> ServiceRecordItems => Set<ServiceRecordItem>();
    public DbSet<FuelEntry> FuelEntries => Set<FuelEntry>();
    public DbSet<Reminder> Reminders => Set<Reminder>();
    public DbSet<Document> Documents => Set<Document>();
    public DbSet<PushSubscription> PushSubscriptions => Set<PushSubscription>();
    public DbSet<SentNotification> SentNotifications => Set<SentNotification>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.HasDefaultSchema("garage");

        builder.ApplyConfigurationsFromAssembly(typeof(GarageDbContext).Assembly);

        // Every aggregate assigns its own Id in the constructor. Without this, EF sees a
        // non-default key on a child discovered through a tracked parent's navigation,
        // concludes the row already exists, and issues an UPDATE that matches nothing
        // instead of an INSERT.
        foreach (var entityType in builder.Model.GetEntityTypes()
                     .Where(t => typeof(Entity).IsAssignableFrom(t.ClrType)))
        {
            builder.Entity(entityType.ClrType)
                .Property(nameof(Entity.Id))
                .ValueGeneratedNever();
        }
    }
}
