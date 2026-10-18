using EventPlanner.Core.Models.Events;
using EventPlanner.Core.Models.Tasks;
using EventPlanner.Core.Validation;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EventPlanner.Infrastructure.Data.Configurations;

internal sealed class EventTaskConfiguration : IEntityTypeConfiguration<EventTask>
{
    public void Configure(EntityTypeBuilder<EventTask> builder)
    {
        builder.ToTable("Tasks");
        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id).ValueGeneratedNever();
        builder.Property(t => t.Title).IsRequired().HasMaxLength(TextValidation.NameMaxLength);
        builder.Property(t => t.OwnerLabel).HasMaxLength(TextValidation.NameMaxLength);
        builder.Property(t => t.Notes).HasMaxLength(TextValidation.ShortTextMaxLength);
        builder.Property(t => t.State).HasConversion<string>().HasMaxLength(32);
        builder.Property(t => t.Priority).HasConversion<string>().HasMaxLength(32);
        builder.Property(t => t.Revision).IsConcurrencyToken();

        builder.HasOne<EventProject>()
            .WithMany()
            .HasForeignKey(t => t.EventId)
            .OnDelete(DeleteBehavior.Cascade);

        // Deleting a task keeps the tasks that followed it; they only lose the dependency.
        builder.HasOne<EventTask>()
            .WithMany()
            .HasForeignKey(t => t.PredecessorId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(t => new { t.EventId, t.State, t.DueLocalDate });
        builder.HasIndex(t => t.PredecessorId);
    }
}
