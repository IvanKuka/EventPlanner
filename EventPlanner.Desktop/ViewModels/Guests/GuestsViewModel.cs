using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EventPlanner.Contracts.Events;
using EventPlanner.Contracts.Guests;
using EventPlanner.Contracts.Results;
using EventPlanner.Core.Services.Guests;
using EventPlanner.Desktop.Localization;
using EventPlanner.Desktop.ViewModels.Shell;

namespace EventPlanner.Desktop.ViewModels.Guests;

/// <summary>
/// Guests and groups of the active project: filtered list, guest editor with an explicitly revealed
/// private note, group editor and group RSVP with per-member exceptions.
/// </summary>
public sealed partial class GuestsViewModel : ViewModelBase, IPage
{
    private static readonly TimeSpan SearchDebounce = TimeSpan.FromMilliseconds(250);

    private readonly GuestService _guests;
    private readonly ActiveProjectContext _active;
    private EventProjectDto? _project;
    private List<GuestDto> _allGuests = [];
    private List<GroupDto> _allGroups = [];
    private GuestDto? _editing;
    private CancellationTokenSource? _searchCts;

    public GuestsViewModel(GuestService guests, ActiveProjectContext active)
    {
        _guests = guests;
        _active = active;
        RsvpFilter = RsvpFilterOptions[0];
        PartFilter = AllParts;
        GroupFilter = AllGroups;
        EditKind = KindOptions[0];
        EditRsvp = RsvpOptions[0];
        EditGroup = NoGroup;
        GroupRsvpState = RsvpOptions.First(o => o.Value == RsvpState.Accepted);
    }

    public string Title => Strings.Nav_Guests;

    private static DisplayOption<Guid?> AllParts { get; } = new(null, Strings.Guests_FilterAllParts);

    private static DisplayOption<Guid?> AllGroups { get; } = new(null, Strings.Guests_FilterAllGroups);

    private static DisplayOption<Guid?> NoGroup { get; } = new(null, Strings.Guests_NoGroup);

    public IReadOnlyList<DisplayOption<RsvpState?>> RsvpFilterOptions { get; } =
        [new(null, Strings.Guests_FilterAllRsvp), .. Enum.GetValues<RsvpState>().Select(s => new DisplayOption<RsvpState?>(s, EnumText.For(s)))];

    public IReadOnlyList<DisplayOption<RsvpState>> RsvpOptions { get; } = EnumText.Options<RsvpState>(EnumText.For);

    public IReadOnlyList<DisplayOption<GuestKind>> KindOptions { get; } = EnumText.Options<GuestKind>(EnumText.For);

    public ObservableCollection<DisplayOption<Guid?>> PartFilterOptions { get; } = [];

    public ObservableCollection<DisplayOption<Guid?>> GroupFilterOptions { get; } = [];

    public ObservableCollection<DisplayOption<Guid?>> GroupOptions { get; } = [];

    public ObservableCollection<GuestRow> Guests { get; } = [];

    public ObservableCollection<GroupDto> Groups { get; } = [];

    public ObservableCollection<Choice> PartChoices { get; } = [];

    public ObservableCollection<Choice> GroupMembers { get; } = [];

    public ObservableCollection<ProgramPartCountsDto> Counts { get; } = [];

    [ObservableProperty]
    public partial bool HasProject { get; set; }

    [ObservableProperty]
    public partial bool IsReadOnly { get; set; }

    [ObservableProperty]
    public partial string SearchText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial DisplayOption<RsvpState?> RsvpFilter { get; set; }

    [ObservableProperty]
    public partial DisplayOption<Guid?> PartFilter { get; set; }

    [ObservableProperty]
    public partial DisplayOption<Guid?> GroupFilter { get; set; }

    [ObservableProperty]
    public partial string ListSummary { get; set; } = string.Empty;

    [ObservableProperty]
    public partial GuestRow? SelectedGuest { get; set; }

    [ObservableProperty]
    public partial bool IsEditingGuest { get; set; }

    [ObservableProperty]
    public partial string EditName { get; set; } = string.Empty;

    [ObservableProperty]
    public partial DisplayOption<GuestKind> EditKind { get; set; }

    [ObservableProperty]
    public partial string? EditAgeCategory { get; set; }

    [ObservableProperty]
    public partial DisplayOption<RsvpState> EditRsvp { get; set; }

    [ObservableProperty]
    public partial DisplayOption<Guid?> EditGroup { get; set; }

    [ObservableProperty]
    public partial string? EditPrivateNote { get; set; }

    [ObservableProperty]
    public partial bool IsPrivateNoteRevealed { get; set; }

    [ObservableProperty]
    public partial bool EditingHasPrivateNote { get; set; }

    [ObservableProperty]
    public partial bool IsConfirmingDelete { get; set; }

    /// <summary>Deleting a group keeps its guests but drops their membership; it still asks first.</summary>
    [ObservableProperty]
    public partial bool IsConfirmingGroupDelete { get; set; }

    [ObservableProperty]
    public partial GroupDto? SelectedGroup { get; set; }

    [ObservableProperty]
    public partial bool IsEditingGroup { get; set; }

    [ObservableProperty]
    public partial string GroupLabel { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string? GroupContactName { get; set; }

    [ObservableProperty]
    public partial string? GroupContactEmail { get; set; }

    [ObservableProperty]
    public partial string? GroupPhone { get; set; }

    [ObservableProperty]
    public partial string? GroupNotes { get; set; }

    [ObservableProperty]
    public partial DisplayOption<RsvpState> GroupRsvpState { get; set; }

    [ObservableProperty]
    public partial FieldErrors Errors { get; set; } = FieldErrors.None;

    [ObservableProperty]
    public partial string? ErrorMessage { get; set; }

    [ObservableProperty]
    public partial string? InfoMessage { get; set; }

    [ObservableProperty]
    public partial string? WarningMessage { get; set; }

    [ObservableProperty]
    public partial string? UnassignedText { get; set; }

    public async Task ActivateAsync(CancellationToken cancellationToken)
    {
        _project = _active.Project;
        HasProject = _project is not null;
        IsReadOnly = _project?.IsReadOnly ?? false;
        if (_project is null)
        {
            Guests.Clear();
            Groups.Clear();
            return;
        }

        await ReloadAsync(_project, cancellationToken);
    }

    partial void OnSearchTextChanged(string value)
    {
        _searchCts?.Cancel();
        _searchCts = new CancellationTokenSource();
        _ = DebouncedFilterAsync(_searchCts.Token);
    }

    partial void OnRsvpFilterChanged(DisplayOption<RsvpState?> value) => ApplyFilter();

    partial void OnPartFilterChanged(DisplayOption<Guid?> value) => ApplyFilter();

    partial void OnGroupFilterChanged(DisplayOption<Guid?> value) => ApplyFilter();

    partial void OnSelectedGuestChanged(GuestRow? value)
    {
        if (value is not null)
        {
            EditGuest(value.Guest);
        }
    }

    partial void OnSelectedGroupChanged(GroupDto? value)
    {
        if (value is not null)
        {
            EditExistingGroup(value);
        }
    }

    [RelayCommand]
    private void NewGuest()
    {
        SelectedGuest = null;
        SelectedGroup = null;
        _editing = null;
        IsEditingGroup = false;
        IsEditingGuest = true;
        EditName = string.Empty;
        EditKind = KindOptions[0];
        EditAgeCategory = null;
        EditRsvp = RsvpOptions.First(o => o.Value == RsvpState.Invited);
        EditGroup = GroupFilter.Value is { } groupId ? GroupOptions.First(o => o.Value == groupId) : NoGroup;
        EditPrivateNote = null;
        EditingHasPrivateNote = false;
        IsPrivateNoteRevealed = true;
        SetPartChoices([]);
        ClearMessages();
    }

    [RelayCommand]
    private async Task RevealPrivateNoteAsync()
    {
        if (_editing is null)
        {
            return;
        }

        var detail = await _guests.GetGuestDetailAsync(_editing.Id, CancellationToken.None);
        if (!detail.IsSuccess)
        {
            ErrorMessage = ErrorText.For(detail.Error!);
            return;
        }

        EditPrivateNote = detail.Value.PrivateNote;
        IsPrivateNoteRevealed = true;
    }

    [RelayCommand]
    private async Task SaveGuestAsync()
    {
        if (_project is null)
        {
            return;
        }

        var parts = PartChoices.Where(c => c.IsSelected).Select(c => c.Id).ToList();
        Result<GuestDto> result;
        if (_editing is null)
        {
            result = await _guests.CreateGuestAsync(
                new CreateGuest(_project.Id, EditGroup.Value, EditName, EditKind.Value, EditAgeCategory, EditRsvp.Value, parts, EditPrivateNote),
                CancellationToken.None);
        }
        else
        {
            // An unrevealed note must be preserved, so it is loaded right before the update.
            var note = EditPrivateNote;
            if (!IsPrivateNoteRevealed)
            {
                var detail = await _guests.GetGuestDetailAsync(_editing.Id, CancellationToken.None);
                if (!detail.IsSuccess)
                {
                    ErrorMessage = ErrorText.For(detail.Error!);
                    return;
                }

                note = detail.Value.PrivateNote;
            }

            result = await _guests.UpdateGuestAsync(
                new UpdateGuest(_editing.Id, _editing.Revision, EditGroup.Value, EditName, EditKind.Value, EditAgeCategory, EditRsvp.Value, parts, note),
                CancellationToken.None);
        }

        Errors = ErrorText.FieldsOf(result.Error);
        if (!result.IsSuccess)
        {
            ErrorMessage = ErrorText.For(result.Error!);
            return;
        }

        ErrorMessage = null;
        InfoMessage = Strings.Info_Saved;
        WarningMessage = result.Warnings.Count > 0 ? string.Join(" ", result.Warnings.Select(ErrorText.For)) : null;
        await ReloadAsync(_project, CancellationToken.None, selectGuestId: result.Value.Id);
    }

    [RelayCommand]
    private void RequestDeleteGuest() => IsConfirmingDelete = _editing is not null;

    [RelayCommand]
    private void CancelDeleteGuest() => IsConfirmingDelete = false;

    [RelayCommand]
    private async Task ConfirmDeleteGuestAsync()
    {
        IsConfirmingDelete = false;
        if (_editing is null || _project is null)
        {
            return;
        }

        var result = await _guests.DeleteGuestAsync(new DeleteGuest(_editing.Id, _editing.Revision), CancellationToken.None);
        if (!result.IsSuccess)
        {
            ErrorMessage = ErrorText.For(result.Error!);
            return;
        }

        IsEditingGuest = false;
        _editing = null;
        InfoMessage = Strings.Info_Deleted;
        await ReloadAsync(_project, CancellationToken.None);
    }

    [RelayCommand]
    private void NewGroup()
    {
        SelectedGroup = null;
        IsEditingGuest = false;
        IsEditingGroup = true;
        GroupLabel = string.Empty;
        GroupContactName = null;
        GroupContactEmail = null;
        GroupPhone = null;
        GroupNotes = null;
        GroupMembers.Clear();
        ClearMessages();
    }

    [RelayCommand]
    private async Task SaveGroupAsync()
    {
        if (_project is null)
        {
            return;
        }

        var result = SelectedGroup is null
            ? await _guests.CreateGroupAsync(
                new CreateGroup(_project.Id, GroupLabel, GroupContactName, GroupContactEmail, GroupPhone, GroupNotes), CancellationToken.None)
            : await _guests.UpdateGroupAsync(
                new UpdateGroup(SelectedGroup.Id, GroupLabel, GroupContactName, GroupContactEmail, GroupPhone, GroupNotes), CancellationToken.None);

        Errors = ErrorText.FieldsOf(result.Error);
        if (!result.IsSuccess)
        {
            ErrorMessage = ErrorText.For(result.Error!);
            return;
        }

        ErrorMessage = null;
        InfoMessage = Strings.Info_Saved;
        await ReloadAsync(_project, CancellationToken.None, selectGroupId: result.Value.Id);
    }

    [RelayCommand]
    private void RequestDeleteGroup() => IsConfirmingGroupDelete = SelectedGroup is not null;

    [RelayCommand]
    private void CancelDeleteGroup() => IsConfirmingGroupDelete = false;

    [RelayCommand]
    private async Task DeleteGroupAsync()
    {
        IsConfirmingGroupDelete = false;
        if (SelectedGroup is null || _project is null)
        {
            return;
        }

        var result = await _guests.DeleteGroupAsync(new DeleteGroup(SelectedGroup.Id), CancellationToken.None);
        if (!result.IsSuccess)
        {
            ErrorMessage = ErrorText.For(result.Error!);
            return;
        }

        IsEditingGroup = false;
        InfoMessage = Strings.Guests_GroupDeleted;
        await ReloadAsync(_project, CancellationToken.None);
    }

    [RelayCommand]
    private async Task ApplyGroupRsvpAsync()
    {
        if (SelectedGroup is null || _project is null)
        {
            return;
        }

        var targets = GroupMembers.Where(m => m.IsSelected).Select(m => new GuestRevision(m.Id, m.Revision)).ToList();
        var result = await _guests.SetGroupRsvpAsync(new SetGroupRsvp(SelectedGroup.Id, GroupRsvpState.Value, targets), CancellationToken.None);
        if (!result.IsSuccess)
        {
            ErrorMessage = ErrorText.For(result.Error!);
            return;
        }

        ErrorMessage = null;
        InfoMessage = string.Format(CultureInfo.CurrentCulture, Strings.Guests_GroupRsvpApplied, result.Value.Count, GroupRsvpState.Label);
        await ReloadAsync(_project, CancellationToken.None, selectGroupId: SelectedGroup.Id);
    }

    private async Task ReloadAsync(EventProjectDto project, CancellationToken cancellationToken, Guid? selectGuestId = null, Guid? selectGroupId = null)
    {
        var guestsTask = _guests.ListGuestsAsync(project.Id, cancellationToken);
        var groupsTask = _guests.ListGroupsAsync(project.Id, cancellationToken);
        var countsTask = _guests.GetCountsAsync(project.Id, cancellationToken);
        await Task.WhenAll(guestsTask, groupsTask, countsTask);
        cancellationToken.ThrowIfCancellationRequested();
        if (_active.Project?.Id != project.Id)
        {
            return; // Results for a project that is no longer active are dropped.
        }

        _allGuests = [.. guestsTask.Result];
        _allGroups = [.. groupsTask.Result];
        RebuildOptions(project);

        Groups.Clear();
        foreach (var group in _allGroups)
        {
            Groups.Add(group);
        }

        Counts.Clear();
        if (countsTask.Result.IsSuccess)
        {
            var counts = countsTask.Result.Value;
            foreach (var part in counts.Parts)
            {
                Counts.Add(part);
            }

            UnassignedText = counts.GuestsWithoutProgramPart > 0
                ? string.Format(CultureInfo.CurrentCulture, Strings.Overview_IssueGuestsWithoutPart, counts.GuestsWithoutProgramPart)
                : null;
        }

        ApplyFilter();

        if (selectGuestId is { } guestId)
        {
            SelectedGuest = Guests.FirstOrDefault(g => g.Guest.Id == guestId);
        }

        // DTOs are records: a reloaded, unchanged group equals the old instance, so the selection
        // setter does not fire. The open group editor is therefore refreshed explicitly.
        var groupToShow = selectGroupId ?? (IsEditingGroup ? SelectedGroup?.Id : null);
        if (groupToShow is { } groupId)
        {
            var group = Groups.FirstOrDefault(g => g.Id == groupId);
            SelectedGroup = group;
            if (group is not null)
            {
                EditExistingGroup(group);
            }
        }
    }

    private void RebuildOptions(EventProjectDto project)
    {
        // A bound ComboBox writes null back when its items are cleared, so every choice over these
        // lists is captured first and restored afterwards.
        var partFilter = PartFilter?.Value;
        PartFilterOptions.Clear();
        PartFilterOptions.Add(AllParts);
        foreach (var part in project.ProgramParts)
        {
            PartFilterOptions.Add(new DisplayOption<Guid?>(part.Id, part.Label));
        }

        PartFilter = PartFilterOptions.FirstOrDefault(o => o.Value == partFilter) ?? AllParts;

        var groupFilter = GroupFilter?.Value;
        var editGroup = EditGroup?.Value;
        GroupFilterOptions.Clear();
        GroupFilterOptions.Add(AllGroups);
        GroupOptions.Clear();
        GroupOptions.Add(NoGroup);
        foreach (var group in _allGroups)
        {
            var option = new DisplayOption<Guid?>(group.Id, group.Label);
            GroupFilterOptions.Add(option);
            GroupOptions.Add(option);
        }

        GroupFilter = GroupFilterOptions.FirstOrDefault(o => o.Value == groupFilter) ?? AllGroups;
        EditGroup = GroupOptions.FirstOrDefault(o => o.Value == editGroup) ?? NoGroup;
    }

    private async Task DebouncedFilterAsync(CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(SearchDebounce, cancellationToken);
            ApplyFilter();
        }
        catch (OperationCanceledException)
        {
            // A newer keystroke replaced this search.
        }
    }

    private void ApplyFilter()
    {
        if (_project is null)
        {
            return;
        }

        var groupsById = _allGroups.ToDictionary(g => g.Id);
        var partsById = _project.ProgramParts.ToDictionary(p => p.Id, p => p.Label);
        var search = SearchText.Trim();

        IEnumerable<GuestDto> query = _allGuests;
        if (RsvpFilter.Value is { } rsvp)
        {
            query = query.Where(g => g.RsvpState == rsvp);
        }

        if (PartFilter.Value is { } partId)
        {
            query = query.Where(g => g.ProgramPartIds.Contains(partId));
        }

        if (GroupFilter.Value is { } groupId)
        {
            query = query.Where(g => g.GroupId == groupId);
        }

        if (search.Length > 0)
        {
            // Search by guest name and by the group's contact (name, e-mail, phone).
            query = query.Where(g =>
                Contains(g.DisplayName, search)
                || (g.GroupId is { } gid && groupsById.TryGetValue(gid, out var group)
                    && (Contains(group.Label, search) || Contains(group.ContactName, search)
                        || Contains(group.ContactEmail, search) || Contains(group.Phone, search))));
        }

        var selectedId = SelectedGuest?.Guest.Id;
        Guests.Clear();
        foreach (var guest in query)
        {
            var parts = string.Join(", ", guest.ProgramPartIds.Where(partsById.ContainsKey).Select(id => partsById[id]));
            Guests.Add(new GuestRow(
                guest,
                EnumText.For(guest.RsvpState),
                guest.GroupId is { } gid && groupsById.TryGetValue(gid, out var group) ? group.Label : null,
                parts.Length > 0 ? parts : Strings.Guests_NoPartAssigned));
        }

        ListSummary = string.Format(CultureInfo.CurrentCulture, Strings.Guests_ListSummary, Guests.Count, _allGuests.Count);
        if (selectedId is { } id)
        {
            SelectedGuest = Guests.FirstOrDefault(g => g.Guest.Id == id);
        }
    }

    private void EditGuest(GuestDto guest)
    {
        _editing = guest;

        // Clear the group selection so clicking the same group again reopens its editor.
        SelectedGroup = null;
        IsEditingGroup = false;
        IsEditingGuest = true;
        IsConfirmingDelete = false;
        EditName = guest.DisplayName;
        EditKind = KindOptions.First(o => o.Value == guest.Kind);
        EditAgeCategory = guest.AgeCategory;
        EditRsvp = RsvpOptions.First(o => o.Value == guest.RsvpState);
        EditGroup = GroupOptions.FirstOrDefault(o => o.Value == guest.GroupId) ?? NoGroup;

        // Sensitive text is not shown until the user explicitly reveals it.
        EditPrivateNote = null;
        EditingHasPrivateNote = guest.HasPrivateNote;
        IsPrivateNoteRevealed = !guest.HasPrivateNote;
        SetPartChoices(guest.ProgramPartIds);
        Errors = FieldErrors.None;
    }

    private void EditExistingGroup(GroupDto group)
    {
        IsConfirmingGroupDelete = false;
        // Only one editor is active; a stale guest selection must not take over after a reload.
        SelectedGuest = null;
        _editing = null;
        IsEditingGuest = false;
        IsEditingGroup = true;
        GroupLabel = group.Label;
        GroupContactName = group.ContactName;
        GroupContactEmail = group.ContactEmail;
        GroupPhone = group.Phone;
        GroupNotes = group.Notes;

        // Every member is listed and individually uncheckable: group RSVP never forces the whole group.
        GroupMembers.Clear();
        foreach (var member in _allGuests.Where(g => g.GroupId == group.Id))
        {
            GroupMembers.Add(new Choice(member.Id, $"{member.DisplayName} ({EnumText.For(member.RsvpState)})", true, member.Revision));
        }

        Errors = FieldErrors.None;
    }

    private void SetPartChoices(IReadOnlyCollection<Guid> selected)
    {
        PartChoices.Clear();
        foreach (var part in _project?.ProgramParts ?? [])
        {
            PartChoices.Add(new Choice(part.Id, part.Label, selected.Contains(part.Id)));
        }
    }

    private void ClearMessages()
    {
        IsConfirmingGroupDelete = false;
        Errors = FieldErrors.None;
        ErrorMessage = null;
        InfoMessage = null;
        WarningMessage = null;
    }

    private static bool Contains(string? text, string search) =>
        text is not null && text.Contains(search, StringComparison.CurrentCultureIgnoreCase);
}
