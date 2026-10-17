using EventPlanner.Core.Models.Events;
using EventPlanner.Core.Models.Guests;
using EventPlanner.Core.Validation;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EventPlanner.Infrastructure.Data.Configurations;

internal sealed class ParticipantGroupConfiguration : IEntityTypeConfiguration<ParticipantGroup>
{
    public void Configure(EntityTypeBuilder<ParticipantGroup> builder)
    {
        builder.ToTable("ParticipantGroups");
        builder.HasKey(g => g.Id);
        builder.Property(g => g.Id).ValueGeneratedNever();
        builder.Property(g => g.Label).IsRequired().HasMaxLength(TextValidation.NameMaxLength);
        builder.Property(g => g.ContactName).HasMaxLength(TextValidation.NameMaxLength);
        builder.Property(g => g.ContactEmail).HasMaxLength(254);
        builder.Property(g => g.Phone).HasMaxLength(40);
        builder.Property(g => g.Notes).HasMaxLength(TextValidation.ShortTextMaxLength);

        builder.HasOne<EventProject>()
            .WithMany()
            .HasForeignKey(g => g.EventId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(g => g.EventId);
    }
}
