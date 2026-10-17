using EventPlanner.Core.Models.Events;
using EventPlanner.Core.Validation;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EventPlanner.Infrastructure.Data.Configurations;

internal sealed class EventProjectConfiguration : IEntityTypeConfiguration<EventProject>
{
    public void Configure(EntityTypeBuilder<EventProject> builder)
    {
        builder.ToTable("EventProjects");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).ValueGeneratedNever();
        builder.Property(e => e.Name).IsRequired().HasMaxLength(TextValidation.NameMaxLength);
        builder.Property(e => e.EventType).HasConversion<string>().HasMaxLength(32);
        builder.Property(e => e.Status).HasConversion<string>().HasMaxLength(32);
        builder.Property(e => e.TimeZoneId).IsRequired().HasMaxLength(64);
        builder.Property(e => e.VenueText).HasMaxLength(TextValidation.ShortTextMaxLength);
        builder.Property(e => e.Currency).IsRequired().HasMaxLength(3);
        builder.Property(e => e.Revision).IsConcurrencyToken();
        builder.Ignore(e => e.IsReadOnly);

        builder.HasMany(e => e.ProgramParts)
            .WithOne()
            .HasForeignKey(p => p.EventId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(e => e.ProgramParts).HasField("_programParts").UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
