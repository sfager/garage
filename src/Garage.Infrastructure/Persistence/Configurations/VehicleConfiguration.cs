using Garage.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Garage.Infrastructure.Persistence.Configurations;

public class HouseholdConfiguration : IEntityTypeConfiguration<Household>
{
    public void Configure(EntityTypeBuilder<Household> builder)
    {
        builder.ToTable("Households");
        builder.HasKey(h => h.Id);
        builder.Property(h => h.Name).HasMaxLength(120).IsRequired();

        builder.HasMany(h => h.Vehicles)
            .WithOne(v => v.Household!)
            .HasForeignKey(v => v.HouseholdId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(h => h.Vehicles).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

public class VehicleConfiguration : IEntityTypeConfiguration<Vehicle>
{
    public void Configure(EntityTypeBuilder<Vehicle> builder)
    {
        builder.ToTable("Vehicles");
        builder.HasKey(v => v.Id);

        builder.Property(v => v.Nickname).HasMaxLength(60).IsRequired();
        builder.Property(v => v.Make).HasMaxLength(60);
        builder.Property(v => v.Model).HasMaxLength(60);
        builder.Property(v => v.Trim).HasMaxLength(60);
        builder.Property(v => v.Engine).HasMaxLength(60);
        builder.Property(v => v.Vin).HasMaxLength(17);
        builder.Property(v => v.LicensePlate).HasMaxLength(12);
        builder.Property(v => v.PhotoPath).HasMaxLength(400);

        // Computed from other columns — not a stored value.
        builder.Ignore(v => v.DisplayName);

        builder.HasIndex(v => new { v.HouseholdId, v.IsArchived });
        builder.HasIndex(v => v.Vin);

        // The aggregate exposes its children as IReadOnlyCollection over backing fields,
        // so EF has to write through the field rather than the read-only property.
        builder.HasMany(v => v.OdometerReadings).WithOne(r => r.Vehicle!)
            .HasForeignKey(r => r.VehicleId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(v => v.Trips).WithOne(t => t.Vehicle!)
            .HasForeignKey(t => t.VehicleId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(v => v.ServiceRecords).WithOne(s => s.Vehicle!)
            .HasForeignKey(s => s.VehicleId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(v => v.FuelEntries).WithOne(f => f.Vehicle!)
            .HasForeignKey(f => f.VehicleId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(v => v.Reminders).WithOne(r => r.Vehicle!)
            .HasForeignKey(r => r.VehicleId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(v => v.Documents).WithOne(d => d.Vehicle!)
            .HasForeignKey(d => d.VehicleId).OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(v => v.OdometerReadings).UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.Navigation(v => v.Trips).UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.Navigation(v => v.ServiceRecords).UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.Navigation(v => v.FuelEntries).UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.Navigation(v => v.Reminders).UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.Navigation(v => v.Documents).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
