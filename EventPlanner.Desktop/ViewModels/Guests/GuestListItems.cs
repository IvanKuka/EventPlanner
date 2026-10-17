using CommunityToolkit.Mvvm.ComponentModel;
using EventPlanner.Contracts.Guests;

namespace EventPlanner.Desktop.ViewModels.Guests;

/// <summary>One line in the guest list. Carries no private note content, only whether one exists.</summary>
public sealed record GuestRow(GuestDto Guest, string RsvpLabel, string? GroupLabel, string PartsLabel)
{
    public string DisplayName => Guest.DisplayName;

    public bool HasPrivateNote => Guest.HasPrivateNote;

    public bool IsPlaceholder => Guest.Kind == GuestKind.PlusOnePlaceholder;
}

/// <summary>A checkbox for a selectable item (program part in the editor, member in group RSVP).</summary>
public sealed partial class Choice(Guid id, string label, bool isSelected, long revision = 0) : ObservableObject
{
    public Guid Id { get; } = id;

    public string Label { get; } = label;

    public long Revision { get; } = revision;

    [ObservableProperty]
    public partial bool IsSelected { get; set; } = isSelected;
}
