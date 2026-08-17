using Garage.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Garage.Infrastructure.Persistence.Configurations;

public class HouseholdInvitationConfiguration : IEntityTypeConfiguration<HouseholdInvitation>
{
    public void Configure(EntityTypeBuilder<HouseholdInvitation> builder)
    {
        builder.ToTable("HouseholdInvitations");
        builder.HasKey(i => i.Id);

        // A SHA-256 hex digest. The code itself is never stored.
        builder.Property(i => i.CodeHash).HasMaxLength(64).IsRequired();
        builder.Property(i => i.CreatedByUserId).HasMaxLength(450).IsRequired();
        builder.Property(i => i.AcceptedByUserId).HasMaxLength(450);

        // Redeeming an invitation looks it up by hash and nothing else.
        builder.HasIndex(i => i.CodeHash).IsUnique();
        builder.HasIndex(i => i.HouseholdId);

        builder.HasOne<Household>()
            .WithMany()
            .HasForeignKey(i => i.HouseholdId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
