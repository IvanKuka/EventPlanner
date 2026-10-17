using EventPlanner.Core.Models.Vendors;
using EventPlanner.Core.Validation;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EventPlanner.Infrastructure.Data.Configurations;

internal sealed class QuoteConfiguration : IEntityTypeConfiguration<Quote>
{
    public void Configure(EntityTypeBuilder<Quote> builder)
    {
        builder.ToTable("Quotes");
        builder.HasKey(q => q.Id);
        builder.Property(q => q.Id).ValueGeneratedNever();
        builder.Property(q => q.Description).IsRequired().HasMaxLength(TextValidation.NameMaxLength);
        builder.Property(q => q.Currency).IsRequired().HasMaxLength(3);
        builder.Property(q => q.ScopeText).IsRequired().HasMaxLength(TextValidation.ShortTextMaxLength);
        builder.Property(q => q.ExclusionsText).HasMaxLength(TextValidation.ShortTextMaxLength);
        builder.Property(q => q.Transport).HasConversion<string>().HasMaxLength(32);
        builder.Property(q => q.Status).HasConversion<string>().HasMaxLength(32);
        builder.Property(q => q.Revision).IsConcurrencyToken();
        builder.Ignore(q => q.KnownTotalMinor);

        builder.HasOne<Vendor>()
            .WithMany()
            .HasForeignKey(q => q.VendorId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(q => new { q.VendorId, q.Status });
    }
}
