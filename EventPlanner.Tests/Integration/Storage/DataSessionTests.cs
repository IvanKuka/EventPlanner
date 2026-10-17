using EventPlanner.Contracts.Guests;
using EventPlanner.Contracts.Results;
using EventPlanner.Core.Models.Guests;
using EventPlanner.Core.Ports;
using EventPlanner.Tests.Fixtures;

namespace EventPlanner.Tests.Integration.Storage;

public sealed class DataSessionTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>A record whose parent vanished meanwhile (e.g. the project was deleted) is a conflict, not a crash.</summary>
    [Fact]
    public async Task Reference_to_a_deleted_parent_is_reported_as_conflict()
    {
        await using var profile = await TestProfile.CreateAsync();
        await using var session = profile.Sessions.Open();
        session.Guests.AddGroup(ParticipantGroup.Create(Guid.CreateVersion7(), Guid.NewGuid(), "Rodina", null, null, null, null));

        Assert.Equal(SaveOutcome.Conflict, await session.SaveChangesAsync(Ct));
    }

    [Fact]
    public async Task Creating_in_a_deleted_project_is_refused()
    {
        await using var profile = await TestProfile.CreateAsync();
        var project = (await profile.Events.CreateAsync(Commands.Wedding(), Ct)).Value;
        Assert.True((await profile.Events.DeleteAsync(new Contracts.Events.DeleteEventProject(project.Id, project.Revision, project.Name), Ct)).IsSuccess);

        var result = await profile.Guests.CreateGroupAsync(new CreateGroup(project.Id, "Rodina", null, null, null, null), Ct);

        Assert.Equal(ErrorCode.NotFound, result.Error?.Code);
    }
}
