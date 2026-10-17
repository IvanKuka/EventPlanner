namespace EventPlanner.Contracts.Results;

/// <summary>
/// Expected failure of a command. <see cref="Detail"/> is a safe developer-facing description
/// without user payload; the UI shows a localized text derived from <see cref="Code"/> instead.
/// </summary>
public sealed record OperationError(ErrorCode Code, string Detail, IReadOnlyList<ValidationIssue> Issues)
{
    public static OperationError Validation(IReadOnlyList<ValidationIssue> issues) =>
        new(ErrorCode.ValidationFailed, "Validation failed.", issues);

    public static OperationError Validation(string field, string rule) =>
        Validation([new ValidationIssue(field, rule)]);

    public static OperationError NotFound(string entity) =>
        new(ErrorCode.NotFound, $"{entity} was not found.", []);

    public static OperationError Conflict(string entity) =>
        new(ErrorCode.Conflict, $"{entity} was changed by another operation.", []);

    public static OperationError ReadOnly(string reason) =>
        new(ErrorCode.ReadOnly, reason, []);

    public static OperationError Of(ErrorCode code, string detail) => new(code, detail, []);
}
