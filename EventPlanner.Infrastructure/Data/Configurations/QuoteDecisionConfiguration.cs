using EventPlanner.Core.Models.Vendors;
using EventPlanner.Core.Validation;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EventPlanner.Infrastructure.Data.Configurations;

internal sealed class QuoteDecisionConfiguration : IEntityTypeConfiguration<QuoteDecision>
{
    public void Configure(EntityTypeBuilder<QuoteDecision> builder)
    {
        builder.ToTable("QuoteDecisions");
        builder.HasKey(d => d.Id);
        builder.Property(d => d.Id).ValueGeneratedNever();
        builder.Property(d => d.Kind).HasConversion<string>().HasMaxLength(32);
        builder.Property(d => d.Currency).IsRequired().HasMaxLength(3);
        builder.Property(d => d.BudgetImpact).HasConversion<string>().HasMaxLength(32);
        builder.Property(d => d.Note).HasMaxLength(TextValidation.ShortTextMaxLength);

        // History belongs to its quote: deleting the quote (or its vendor) deletes its decisions.
        builder.HasOne<Quote>()
            .WithMany()
            .HasForeignKey(d => d.QuoteId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(d => d.QuoteId);
    }
}
