namespace EventPlanner.Desktop.Composition;

/// <summary>
/// Decides which unhandled UI exceptions the app may survive. Every database write is one
/// transaction (SaveChanges), so a failed command leaves the data consistent and at most the shown
/// page stale; that page is reloaded. Runtime faults that leave the process itself unreliable still
/// end the app (after being logged).
/// </summary>
public static class UnexpectedErrors
{
    public static bool CanContinue(Exception exception) => exception is not (
        OutOfMemoryException
        or InsufficientExecutionStackException
        or AccessViolationException
        or BadImageFormatException
        or TypeLoadException
        or InvalidProgramException);
}
