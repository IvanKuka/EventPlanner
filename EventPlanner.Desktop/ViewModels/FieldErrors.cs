namespace EventPlanner.Desktop.ViewModels;

/// <summary>
/// Immutable per-field error texts bound as <c>Errors[Name]</c>. A new instance is assigned after
/// each command so bindings refresh through the owning property's change notification.
/// </summary>
public sealed class FieldErrors
{
    private readonly Dictionary<string, string> _errors;

    public FieldErrors(IEnumerable<KeyValuePair<string, string>> errors)
    {
        _errors = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (field, text) in errors)
        {
            // Keep the first problem per field; it is the most specific one the validator reported.
            _errors.TryAdd(field, text);
        }
    }

    public static FieldErrors None { get; } = new([]);

    public bool HasErrors => _errors.Count > 0;

    public string? this[string field] => _errors.GetValueOrDefault(field);
}
