using EventPlanner.Core.Models.Budget;
using EventPlanner.Core.Models.Events;
using EventPlanner.Core.Models.Vendors;
using EventPlanner.Core.Validation;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EventPlanner.Infrastructure.Data.Configurations;

internal sealed class BudgetItemConfiguration : IEntityTypeConfiguration<BudgetItem>
{
    public void Configure(EntityTypeBuilder<BudgetItem> builder)
    {
        builder.ToTable("BudgetItems");
        builder.HasKey(i => i.Id);
        builder.Property(i => i.Id).ValueGeneratedNever();
        builder.Property(i => i.Category).IsRequired().HasMaxLength(TextValidation.NameMaxLength);
        builder.Property(i => i.Title).IsRequired().HasMaxLength(TextValidation.NameMaxLength);
        builder.Property(i => i.Currency).IsRequired().HasMaxLength(3);
        builder.Property(i => i.Revision).IsConcurrencyToken();
        builder.Ignore(i => i.PostedPaymentsMinor);
        builder.Ignore(i => i.PostedRefundsMinor);

        builder.HasOne<EventProject>()
            .WithMany()
            .HasForeignKey(i => i.EventId)
            .OnDelete(DeleteBehavior.Cascade);

        // Deleting a vendor or quote keeps the budget line and its amounts; only the link is cleared.
        builder.HasOne<Quote>()
            .WithMany()
            .HasForeignKey(i => i.SelectedQuoteId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasMany(i => i.Payments)
            .WithOne()
            .HasForeignKey(p => p.BudgetItemId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(i => i.Payments).HasField("_payments").UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasIndex(i => i.EventId);
        builder.HasIndex(i => i.SelectedQuoteId);
    }
}
