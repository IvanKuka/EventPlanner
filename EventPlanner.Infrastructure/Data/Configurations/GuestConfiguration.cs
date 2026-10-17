using EventPlanner.Core.Models.Events;
using EventPlanner.Core.Models.Guests;
using EventPlanner.Core.Validation;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EventPlanner.Infrastructure.Data.Configurations;

internal sealed class GuestConfiguration : IEntityTypeConfiguration<Guest>
{
    public void Configure(EntityTypeBuilder<Guest> builder)
    {
        builder.ToTable("Guests");
        builder.HasKey(g => g.Id);
        builder.Property(g => g.Id).ValueGeneratedNever();
        builder.Property(g => g.DisplayName).IsRequired().HasMaxLength(TextValidation.NameMaxLength);
        builder.Property(g => g.Kind).HasConversion<string>().HasMaxLength(32);
        builder.Property(g => g.RsvpState).HasConversion<string>().HasMaxLength(32);
        builder.Property(g => g.AgeCategory).HasMaxLength(TextValidation.NameMaxLength);
        builder.Property(g => g.PrivateNote).HasMaxLength(TextValidation.LongTextMaxLength);
        builder.Property(g => g.Revision).IsConcurrencyToken();
        builder.Ignore(g => g.ProgramPartIds);

        builder.HasOne<EventProject>()
            .WithMany()
            .HasForeignKey(g => g.EventId)
            .OnDelete(DeleteBehavior.Cascade);

        // The service unlinks members before deleting a group; SET NULL is only a safety net.
        builder.HasOne<ParticipantGroup>()
            .WithMany()
            .HasForeignKey(g => g.GroupId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasMany(g => g.Attendance)
            .WithOne()
            .HasForeignKey(a => a.GuestId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(g => g.Attendance).HasField("_attendance").UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasIndex(g => new { g.EventId, g.RsvpState });
        builder.HasIndex(g => g.GroupId);
    }
}
