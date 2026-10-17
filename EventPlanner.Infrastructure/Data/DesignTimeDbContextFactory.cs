using Microsoft.EntityFrameworkCore.Design;

namespace EventPlanner.Infrastructure.Data;

/// <summary>Used only by `dotnet ef migrations add`; never touches a user profile.</summary>
internal sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<EventPlannerDbContext>
{
    public EventPlannerDbContext CreateDbContext(string[] args) =>
        new ProfileDatabase(Path.Combine(Path.GetTempPath(), "eventplanner-design-time.db")).CreateContext();
}
