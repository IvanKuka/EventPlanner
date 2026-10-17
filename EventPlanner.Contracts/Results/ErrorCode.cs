namespace EventPlanner.Contracts.Results;

/// <summary>
/// Stable error codes shared by all features. The UI translates them; logs keep them without payload.
/// </summary>
public enum ErrorCode
{
    ValidationFailed,
    Conflict,
    NotFound,
    ReadOnly,
    MissingSource,
    UnsupportedInput,
    PermissionDenied,
    InsufficientSpace,
    CorruptData,
    UnsupportedVersion,
    DependencyUnavailable,
    Interrupted,
    Cancelled,
}
