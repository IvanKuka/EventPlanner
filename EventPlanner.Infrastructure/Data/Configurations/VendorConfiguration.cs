using EventPlanner.Core.Models.Events;
using EventPlanner.Core.Models.Vendors;
using EventPlanner.Core.Validation;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EventPlanner.Infrastructure.Data.Configurations;

internal sealed class VendorConfiguration : IEntityTypeConfiguration<Vendor>
{
    public void Configure(EntityTypeBuilder<Vendor> builder)
    {
        builder.ToTable("Vendors");
        builder.HasKey(v => v.Id);
        builder.Property(v => v.Id).ValueGeneratedNever();
        builder.Property(v => v.Category).IsRequired().HasMaxLength(TextValidation.NameMaxLength);
        builder.Property(v => v.Name).IsRequired().HasMaxLength(TextValidation.NameMaxLength);
        builder.Property(v => v.Contact).HasMaxLength(TextValidation.ShortTextMaxLength);
        builder.Property(v => v.Notes).HasMaxLength(TextValidation.ShortTextMaxLength);
        builder.Property(v => v.Revision).IsConcurrencyToken();

        builder.HasOne<EventProject>()
            .WithMany()
            .HasForeignKey(v => v.EventId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(v => new { v.EventId, v.Category });
    }
}
