namespace HIS.Shared.Results;

/// <summary>
/// Lightweight result wrapper used across CQRS handlers so failures travel as
/// data rather than exceptions. No business value is encoded here.
/// </summary>
public class Result
{
    public bool Succeeded { get; protected set; }
    public string? Error { get; protected set; }

    public static Result Success() => new() { Succeeded = true };
    public static Result Failure(string error) => new() { Succeeded = false, Error = error };
}

public sealed class Result<T> : Result
{
    public T? Value { get; private set; }

    public static Result<T> Success(T value) => new() { Succeeded = true, Value = value };
    public static new Result<T> Failure(string error) => new() { Succeeded = false, Error = error };
}
