namespace CMS.Application.Common;

// I created this Result type to avoid using exceptions for expected business
// failures (like "complaint not found"). The caller always has to check IsSuccess
// before using the value, which makes error handling explicit.
public sealed class Result<T>
{
    public T? Value { get; }
    public string? Error { get; }
    public bool IsSuccess { get; }
    public bool IsFailure => !IsSuccess;

    private Result(T value)      { Value = value; IsSuccess = true; }
    private Result(string error) { Error = error; IsSuccess = false; }

    public static Result<T> Ok(T value)      => new(value);
    public static Result<T> Fail(string error) => new(error);
}

// Non-generic version for operations that don't return a value.
public sealed class Result
{
    public string? Error { get; }
    public bool IsSuccess { get; }
    public bool IsFailure => !IsSuccess;

    private Result(bool success, string? error) { IsSuccess = success; Error = error; }

    public static Result Ok()              => new(true, null);
    public static Result Fail(string error) => new(false, error);
}
