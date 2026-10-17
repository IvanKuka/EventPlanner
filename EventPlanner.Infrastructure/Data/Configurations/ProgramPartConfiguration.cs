using EventPlanner.Core.Models.Events;
using EventPlanner.Core.Validation;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EventPlanner.Infrastructure.Data.Configurations;

internal sealed class ProgramPartConfiguration : IEntityTypeConfiguration<ProgramPart>
{
    public void Configure(EntityTypeBuilder<ProgramPart> builder)
    {
        builder.ToTable("ProgramParts");
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).ValueGeneratedNever();
        builder.Property(p => p.Label).IsRequired().HasMaxLength(TextValidation.NameMaxLength);
        builder.HasIndex(p => new { p.EventId, p.Order });
    }
}
