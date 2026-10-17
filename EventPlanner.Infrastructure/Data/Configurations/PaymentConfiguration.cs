using EventPlanner.Core.Models.Budget;
using EventPlanner.Core.Validation.Budget;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EventPlanner.Infrastructure.Data.Configurations;

internal sealed class PaymentConfiguration : IEntityTypeConfiguration<Payment>
{
    public void Configure(EntityTypeBuilder<Payment> builder)
    {
        builder.ToTable("Payments");
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).ValueGeneratedNever();
        builder.Property(p => p.Kind).HasConversion<string>().HasMaxLength(32);
        builder.Property(p => p.Status).HasConversion<string>().HasMaxLength(32);
        builder.Property(p => p.Reference).HasMaxLength(BudgetValidator.ReferenceMaxLength);
        builder.Property(p => p.Revision).IsConcurrencyToken();

        // The unique operation id is what makes RecordPayment idempotent even under a double submit.
        builder.HasIndex(p => p.OperationId).IsUnique();
        builder.HasIndex(p => new { p.Status, p.DueLocalDate });
        builder.HasIndex(p => p.BudgetItemId);
    }
}
