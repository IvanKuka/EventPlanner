using CommunityToolkit.Mvvm.ComponentModel;
using EventPlanner.Contracts.Events;

namespace EventPlanner.Desktop.ViewModels.Shell;

/// <summary>
/// The project the user is working on, shared by all pages. Pages reload on change and must drop
/// results that arrive for a previously active project.
/// </summary>
public sealed partial class ActiveProjectContext : ObservableObject
{
    [ObservableProperty]
    public partial EventProjectDto? Project { get; set; }
}
