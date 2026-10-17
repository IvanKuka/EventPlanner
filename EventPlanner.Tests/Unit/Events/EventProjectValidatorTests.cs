using EventPlanner.Contracts.Events;
using EventPlanner.Contracts.Results;
using EventPlanner.Core.Models.Events;
using EventPlanner.Core.Validation.Events;

namespace EventPlanner.Tests.Unit.Events;

public sealed class EventProjectValidatorTests
{
    private static readonly DateOnly Start = new(2027, 6, 12);

    [Fact]
    public void Valid_project_has_no_issues()
    {
        var issues = EventProjectValidator.Validate("Svadba", EventType.Wedding, Start, Start, "Europe/Bratislava", null, "EUR");
        Assert.Empty(issues);
    }

    [Fact]
    public void End_before_start_is_rejected_but_multi_day_event_is_allowed()
    {
        var invalid = EventProjectValidator.Validate("X", EventType.Other, Start, Start.AddDays(-1), "Europe/Bratislava", null, "EUR");
        Assert.Contains(invalid, i => i.Rule == ValidationRules.EndBeforeStart);

        var multiDay = EventProjectValidator.Validate("X", EventType.Other, Start, Start.AddDays(2), "Europe/Bratislava", null, "EUR");
        Assert.Empty(multiDay);
    }

    [Theory]
    [InlineData("Central Europe Standard Time")]
    [InlineData("Mars/Olympus")]
    [InlineData("")]
    public void Only_iana_time_zones_are_accepted(string zone)
    {
        var issues = EventProjectValidator.Validate("X", EventType.Other, Start, Start, zone, null, "EUR");
        Assert.Contains(issues, i => i.Field == nameof(EventProjectDto.TimeZoneId));
    }

    [Theory]
    [InlineData("eur")]
    [InlineData("EURO")]
    [InlineData("€")]
    public void Currency_must_be_iso_code(string currency)
    {
        var issues = EventProjectValidator.Validate("X", EventType.Other, Start, Start, "Europe/Bratislava", null, currency);
        Assert.Contains(issues, i => i.Rule == ValidationRules.InvalidCurrency);
    }

    [Fact]
    public void Name_limits_and_control_characters_are_enforced()
    {
        Assert.Contains(
            EventProjectValidator.Validate(new string('a', 121), EventType.Other, Start, Start, "UTC", null, "EUR"),
            i => i.Rule == ValidationRules.TooLong);
        Assert.Contains(
            EventProjectValidator.Validate("a\0b", EventType.Other, Start, Start, "UTC", null, "EUR"),
            i => i.Rule == ValidationRules.InvalidCharacters);
        Assert.Contains(
            EventProjectValidator.Validate("   ", EventType.Other, Start, Start, "UTC", null, "EUR"),
            i => i.Rule == ValidationRules.Required);
    }

    [Theory]
    [InlineData(EventStatus.Draft, EventStatus.Planning, true)]
    [InlineData(EventStatus.Planning, EventStatus.Ready, true)]
    [InlineData(EventStatus.Ready, EventStatus.Planning, true)]
    [InlineData(EventStatus.Completed, EventStatus.Archived, true)]
    [InlineData(EventStatus.Archived, EventStatus.Completed, true)]
    [InlineData(EventStatus.Draft, EventStatus.Ready, false)]
    [InlineData(EventStatus.Archived, EventStatus.Planning, false)]
    [InlineData(EventStatus.Planning, EventStatus.Archived, false)]
    public void Status_transitions_follow_lifecycle(EventStatus from, EventStatus to, bool allowed) =>
        Assert.Equal(allowed, EventStatusRules.CanTransition(from, to));
}
