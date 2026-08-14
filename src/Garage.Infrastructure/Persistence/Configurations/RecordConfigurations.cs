using Garage.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Garage.Infrastructure.Persistence.Configurations;

public class OdometerReadingConfiguration : IEntityTypeConfiguration<OdometerReading>
{
    public void Configure(EntityTypeBuilder<OdometerReading> builder)
    {
        builder.ToTable("OdometerReadings");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Note).HasMaxLength(400);
        builder.Property(r => r.Source).HasConversion<string>().HasMaxLength(20);
        builder.HasIndex(r => new { r.VehicleId, r.Date });
    }
}

public class TripConfiguration : IEntityTypeConfiguration<Trip>
{
    public void Configure(EntityTypeBuilder<Trip> builder)
    {
        builder.ToTable("Trips");
        builder.HasKey(t => t.Id);
        builder.Property(t => t.Label).HasMaxLength(120).IsRequired();
        builder.Property(t => t.Purpose).HasConversion<string>().HasMaxLength(20);
        builder.Ignore(t => t.Distance);
        builder.HasIndex(t => new { t.VehicleId, t.Date });
    }
}

public class ServiceRecordConfiguration : IEntityTypeConfiguration<ServiceRecord>
{
    public void Configure(EntityTypeBuilder<ServiceRecord> builder)
    {
        builder.ToTable("ServiceRecords");
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Category).HasConversion<string>().HasMaxLength(30);
        builder.Property(s => s.TotalCost).HasPrecision(10, 2);
        builder.Property(s => s.PartsCost).HasPrecision(10, 2);
        builder.Property(s => s.LaborCost).HasPrecision(10, 2);
        builder.Property(s => s.Shop).HasMaxLength(120);
        builder.Property(s => s.Notes).HasMaxLength(2000);
        builder.Ignore(s => s.Summary);

        builder.HasMany(s => s.Items).WithOne(i => i.ServiceRecord!)
            .HasForeignKey(i => i.ServiceRecordId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(s => s.Receipts).WithOne(d => d.ServiceRecord!)
            .HasForeignKey(d => d.ServiceRecordId).OnDelete(DeleteBehavior.NoAction);

        builder.Navigation(s => s.Items).UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.Navigation(s => s.Receipts).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasIndex(s => new { s.VehicleId, s.Date });
    }
}

public class ServiceRecordItemConfiguration : IEntityTypeConfiguration<ServiceRecordItem>
{
    public void Configure(EntityTypeBuilder<ServiceRecordItem> builder)
    {
        builder.ToTable("ServiceRecordItems");
        builder.HasKey(i => i.Id);
        builder.Property(i => i.Name).HasMaxLength(120).IsRequired();

        // A completed item keeps pointing at its reminder; deleting the reminder must
        // not take the history with it. ClientSetNull keeps SQL Server out of the
        // multiple-cascade-paths error while EF still nulls loaded references.
        builder.HasOne(i => i.Reminder).WithMany()
            .HasForeignKey(i => i.ReminderId).OnDelete(DeleteBehavior.ClientSetNull);
    }
}

public class FuelEntryConfiguration : IEntityTypeConfiguration<FuelEntry>
{
    public void Configure(EntityTypeBuilder<FuelEntry> builder)
    {
        builder.ToTable("FuelEntries");
        builder.HasKey(f => f.Id);
        builder.Property(f => f.Gallons).HasPrecision(8, 3);
        builder.Property(f => f.TotalCost).HasPrecision(10, 2);
        builder.Property(f => f.Station).HasMaxLength(120);
        builder.Ignore(f => f.PricePerGallon);
        builder.HasIndex(f => new { f.VehicleId, f.Odometer });
    }
}

public class ReminderConfiguration : IEntityTypeConfiguration<Reminder>
{
    public void Configure(EntityTypeBuilder<Reminder> builder)
    {
        builder.ToTable("Reminders");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Item).HasMaxLength(120).IsRequired();

        // All four are projected from the intervals and the anchor.
        builder.Ignore(r => r.DueOdometer);
        builder.Ignore(r => r.DueDate);
        builder.Ignore(r => r.TriggerDescription);
        builder.Ignore(r => r.IntervalDescription);

        builder.HasIndex(r => new { r.VehicleId, r.IsDismissed });
    }
}

public class DocumentConfiguration : IEntityTypeConfiguration<Document>
{
    public void Configure(EntityTypeBuilder<Document> builder)
    {
        builder.ToTable("Documents");
        builder.HasKey(d => d.Id);
        builder.Property(d => d.Type).HasConversion<string>().HasMaxLength(20);
        builder.Property(d => d.Title).HasMaxLength(160).IsRequired();
        builder.Property(d => d.FileName).HasMaxLength(260).IsRequired();
        builder.Property(d => d.ContentType).HasMaxLength(120).IsRequired();
        builder.Property(d => d.StoragePath).HasMaxLength(400).IsRequired();
        builder.Ignore(d => d.IsImage);
        builder.HasIndex(d => new { d.VehicleId, d.ExpiresOn });
    }
}
