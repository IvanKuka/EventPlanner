using EventPlanner.Contracts.Tasks;
using EventPlanner.Core.Models.Tasks;
using EventPlanner.Core.Services.Tasks;

namespace EventPlanner.Tests.Unit.Tasks;

public sealed class TaskPlanningTests
{
    private static readonly Guid A = Guid.NewGuid();
    private static readonly Guid B = Guid.NewGuid();
    private static readonly Guid C = Guid.NewGuid();

    [Fact]
    public void Task_cannot_follow_itself_or_close_a_chain_into_a_loop()
    {
        // B follows A, C follows B.
        var predecessorOf = new Dictionary<Guid, Guid?> { [A] = null, [B] = A, [C] = B };

        Assert.True(TaskPlanning.WouldCreateCycle(A, A, predecessorOf));
        Assert.True(TaskPlanning.WouldCreateCycle(A, C, predecessorOf)); // A -> C -> B -> A
        Assert.False(TaskPlanning.WouldCreateCycle(C, A, predecessorOf));
        Assert.False(TaskPlanning.WouldCreateCycle(A, null, predecessorOf));
    }

    [Fact]
    public void Existing_broken_chain_does_not_hang_the_check()
    {
        var predecessorOf = new Dictionary<Guid, Guid?> { [B] = C, [C] = B };

        Assert.True(TaskPlanning.WouldCreateCycle(A, B, predecessorOf));
    }

    [Fact]
    public void Open_tasks_come_first_by_deadline_then_priority_and_done_tasks_last()
    {
        var today = new DateOnly(2027, 5, 1);
        var late = NewTask("Pozvánky", new DateOnly(2027, 4, 1), TaskPriority.Normal);
        var soonHigh = NewTask("Sála", new DateOnly(2027, 5, 10), TaskPriority.High);
        var soonLow = NewTask("Kvety", new DateOnly(2027, 5, 10), TaskPriority.Low);
        var undated = NewTask("Hudba", null, TaskPriority.High);
        var done = NewTask("Fotograf", new DateOnly(2027, 3, 1), TaskPriority.High);
        done.SetState(TaskState.Done);

        var order = TaskPlanning.Order([undated, done, soonLow, late, soonHigh]).Select(t => t.Title);

        Assert.Equal(["Pozvánky", "Sála", "Kvety", "Hudba", "Fotograf"], order);
        Assert.True(TaskPlanning.IsOverdue(late, today));
        Assert.False(TaskPlanning.IsOverdue(done, today)); // a finished task is never overdue
        Assert.False(TaskPlanning.IsOverdue(undated, today));
    }

    [Fact]
    public void Today_is_the_project_date_not_the_computer_date()
    {
        // 23:30 UTC on 1 May is already 2 May in Bratislava (UTC+2 in summer).
        var utc = new DateTimeOffset(2027, 5, 1, 23, 30, 0, TimeSpan.Zero);

        Assert.Equal(new DateOnly(2027, 5, 2), TaskPlanning.Today(utc, "Europe/Bratislava"));
        Assert.Equal(new DateOnly(2027, 5, 1), TaskPlanning.Today(utc, "America/New_York"));
    }

    private static EventTask NewTask(string title, DateOnly? due, TaskPriority priority) =>
        EventTask.Create(Guid.NewGuid(), Guid.Empty, new TaskInput(title, null, due, priority, null, null));
}
