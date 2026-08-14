using Garage.Application.Abstractions;
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
    public DbSet<Vehicle> Vehicles => Set<Vehicle>();
    public DbSet<OdometerReading> OdometerReadings => Set<OdometerReading>();
    public DbSet<Trip> Trips => Set<Trip>();
    public DbSet<ServiceRecord> ServiceRecords => Set<ServiceRecord>();
    public DbSet<ServiceRecordItem> ServiceRecordItems => Set<ServiceRecordItem>();
    public DbSet<FuelEntry> FuelEntries => Set<FuelEntry>();
    public DbSet<Reminder> Reminders => Set<Reminder>();
    public DbSet<Document> Documents => Set<Document>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.ApplyConfigurationsFromAssembly(typeof(GarageDbContext).Assembly);
    }
}
