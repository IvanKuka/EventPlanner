using CommunityToolkit.Mvvm.ComponentModel;

namespace EventPlanner.Desktop.ViewModels;

public abstract class ViewModelBase : ObservableObject;

/// <summary>A navigable page in the shell. Activation reloads data for the current project.</summary>
public interface IPage
{
    string Title { get; }

    Task ActivateAsync(CancellationToken cancellationToken);
}

/// <summary>Localized display wrapper for enum values and optional ids in combo boxes.</summary>
public sealed record DisplayOption<T>(T Value, string Label)
{
    public override string ToString() => Label;
}
