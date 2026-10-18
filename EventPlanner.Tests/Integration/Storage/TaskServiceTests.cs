using EventPlanner.Contracts.Results;
using EventPlanner.Contracts.Tasks;
using EventPlanner.Tests.Fixtures;

namespace EventPlanner.Tests.Integration.Storage;

public sealed class TaskServiceTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Task_list_round_trips_with_owner_deadline_notes_and_state()
    {
        await using var profile = await TestProfile.CreateAsync();
        var project = (await profile.Events.CreateAsync(Commands.Wedding(), Ct)).Value;
        var input = Input("  Objednať tortu ", owner: " Anna ", due: new DateOnly(2000, 1, 1), notes: "chýba počet porcií");
        var created = (await profile.Tasks.CreateAsync(new CreateTask(project.Id, input), Ct)).Value;

        Assert.True((await profile.Tasks.SetStateAsync(new SetTaskState(created.Id, created.Revision, TaskState.InProgress), Ct)).IsSuccess);
        await profile.ReopenAsync();
        var list = (await profile.Tasks.ListAsync(project.Id, Ct)).Value;

        var task = Assert.Single(list.Tasks);
        Assert.Equal(("Objednať tortu", "Anna", "chýba počet porcií", TaskState.InProgress), (task.Title, task.OwnerLabel, task.Notes, task.State));
        Assert.True(task.IsOverdue); // deadline in the past and not done
        Assert.Equal((1, 1), (list.OpenCount, list.OverdueCount));
    }

    [Fact]
    public async Task Dependencies_stay_in_one_event_and_never_form_a_cycle()
    {
        await using var profile = await TestProfile.CreateAsync();
        var project = (await profile.Events.CreateAsync(Commands.Wedding(), Ct)).Value;
        var other = (await profile.Events.CreateAsync(Commands.Wedding("Iná"), Ct)).Value;
        var first = (await profile.Tasks.CreateAsync(new CreateTask(project.Id, Input("Vybrať sálu")), Ct)).Value;
        var second = (await profile.Tasks.CreateAsync(new CreateTask(project.Id, Input("Poslať pozvánky", predecessor: first.Id)), Ct)).Value;
        var foreign = (await profile.Tasks.CreateAsync(new CreateTask(other.Id, Input("Cudzia")), Ct)).Value;

        var cycle = await profile.Tasks.UpdateAsync(new UpdateTask(first.Id, first.Revision, Input("Vybrať sálu", predecessor: second.Id)), Ct);
        var self = await profile.Tasks.UpdateAsync(new UpdateTask(first.Id, first.Revision, Input("Vybrať sálu", predecessor: first.Id)), Ct);
        var crossEvent = await profile.Tasks.CreateAsync(new CreateTask(project.Id, Input("X", predecessor: foreign.Id)), Ct);

        Assert.Equal(ValidationRules.DependencyCycle, Assert.Single(cycle.Error!.Issues).Rule);
        Assert.Equal(ValidationRules.DependencyCycle, Assert.Single(self.Error!.Issues).Rule);
        Assert.Equal(ValidationRules.UnknownReference, Assert.Single(crossEvent.Error!.Issues).Rule);
        Assert.True(second.IsWaiting);
    }

    [Fact]
    public async Task Finishing_before_the_predecessor_warns_and_deleting_it_keeps_the_follower()
    {
        await using var profile = await TestProfile.CreateAsync();
        var project = (await profile.Events.CreateAsync(Commands.Wedding(), Ct)).Value;
        var first = (await profile.Tasks.CreateAsync(new CreateTask(project.Id, Input("Vybrať sálu")), Ct)).Value;
        var second = (await profile.Tasks.CreateAsync(new CreateTask(project.Id, Input("Zaplatiť zálohu", predecessor: first.Id)), Ct)).Value;

        var done = await profile.Tasks.SetStateAsync(new SetTaskState(second.Id, second.Revision, TaskState.Done), Ct);
        Assert.Equal(WarningCodes.PredecessorNotDone, Assert.Single(done.Warnings).Code);

        Assert.True((await profile.Tasks.DeleteAsync(new DeleteTask(first.Id, first.Revision), Ct)).IsSuccess);
        var remaining = Assert.Single((await profile.Tasks.ListAsync(project.Id, Ct)).Value.Tasks);
        Assert.Equal((second.Id, (Guid?)null), (remaining.Id, remaining.PredecessorId));
    }

    [Fact]
    public async Task Stale_revision_invalid_input_and_archived_project_are_refused()
    {
        await using var profile = await TestProfile.CreateAsync();
        var project = (await profile.Events.CreateAsync(Commands.Wedding(), Ct)).Value;
        var task = (await profile.Tasks.CreateAsync(new CreateTask(project.Id, Input("Kvety")), Ct)).Value;
        Assert.True((await profile.Tasks.UpdateAsync(new UpdateTask(task.Id, task.Revision, Input("Kvety a výzdoba")), Ct)).IsSuccess);

        var stale = await profile.Tasks.SetStateAsync(new SetTaskState(task.Id, task.Revision, TaskState.Done), Ct);
        var blank = await profile.Tasks.CreateAsync(new CreateTask(project.Id, Input(" ")), Ct);
        await BudgetServiceTests.ArchiveAsync(profile, project.Id);
        var archived = await profile.Tasks.CreateAsync(new CreateTask(project.Id, Input("Neskoro")), Ct);

        Assert.Equal(ErrorCode.Conflict, stale.Error?.Code);
        Assert.Equal(ValidationRules.Required, Assert.Single(blank.Error!.Issues).Rule);
        Assert.Equal(ErrorCode.ReadOnly, archived.Error?.Code);
    }

    private static TaskInput Input(string title, string? owner = null, DateOnly? due = null, Guid? predecessor = null, string? notes = null) =>
        new(title, owner, due, TaskPriority.Normal, predecessor, notes);
}
