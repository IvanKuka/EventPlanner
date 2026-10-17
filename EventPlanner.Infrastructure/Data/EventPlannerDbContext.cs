using EventPlanner.Core.Models.Budget;
using EventPlanner.Core.Models.Events;
using EventPlanner.Core.Models.Guests;
using EventPlanner.Core.Models.Vendors;
using Microsoft.EntityFrameworkCore;

namespace EventPlanner.Infrastructure.Data;

public sealed class EventPlannerDbContext(DbContextOptions<EventPlannerDbContext> options) : DbContext(options)
{
    public DbSet<EventProject> EventProjects => Set<EventProject>();

    public DbSet<ProgramPart> ProgramParts => Set<ProgramPart>();

    public DbSet<ParticipantGroup> ParticipantGroups => Set<ParticipantGroup>();

    public DbSet<Guest> Guests => Set<Guest>();

    public DbSet<GuestAttendance> GuestAttendances => Set<GuestAttendance>();

    public DbSet<Vendor> Vendors => Set<Vendor>();

    public DbSet<Quote> Quotes => Set<Quote>();

    public DbSet<QuoteDecision> QuoteDecisions => Set<QuoteDecision>();

    public DbSet<BudgetItem> BudgetItems => Set<BudgetItem>();

    public DbSet<Payment> Payments => Set<Payment>();

    protected override void OnModelCreating(ModelBuilder modelBuilder) =>
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(EventPlannerDbContext).Assembly);
}
