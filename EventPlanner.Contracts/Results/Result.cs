namespace EventPlanner.Contracts.Results;

/// <summary>Outcome of a command or query: either a value (with optional warnings) or an error.</summary>
public sealed class Result<T>
{
    private readonly T? _value;

    private Result(T? value, OperationError? error, IReadOnlyList<Warning> warnings)
    {
        _value = value;
        Error = error;
        Warnings = warnings;
    }

    public OperationError? Error { get; }

    public IReadOnlyList<Warning> Warnings { get; }

    public bool IsSuccess => Error is null;

    public T Value => IsSuccess
        ? _value!
        : throw new InvalidOperationException($"Result has no value; error {Error!.Code}.");

    public static Result<T> Success(T value, IReadOnlyList<Warning>? warnings = null) =>
        new(value, null, warnings ?? []);

    public static Result<T> Failure(OperationError error) => new(default, error, []);

    public static implicit operator Result<T>(OperationError error) => Failure(error);

    public static implicit operator Result<T>(T value) => Success(value);
}
