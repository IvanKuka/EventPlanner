using EventPlanner.Core.Models.Events;
using EventPlanner.Core.Models.Guests;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EventPlanner.Infrastructure.Data.Configurations;

internal sealed class GuestAttendanceConfiguration : IEntityTypeConfiguration<GuestAttendance>
{
    public void Configure(EntityTypeBuilder<GuestAttendance> builder)
    {
        builder.ToTable("GuestAttendances");
        builder.HasKey(a => new { a.GuestId, a.ProgramPartId });

        // Removing a program part removes it from every guest's scope in the same transaction.
        builder.HasOne<ProgramPart>()
            .WithMany()
            .HasForeignKey(a => a.ProgramPartId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(a => a.ProgramPartId);
    }
}
