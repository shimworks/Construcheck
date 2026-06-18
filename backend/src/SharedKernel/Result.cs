namespace Construcheck.SharedKernel;

public class Result<T>
{
    public bool IsSuccess { get; }
    public bool IsFailure => !IsSuccess;
    public T? Value { get; }
    public string Error { get; }
    public ResultErrorType ErrorType { get; }

    private Result(T value)
    {
        IsSuccess = true;
        Value = value;
        Error = string.Empty;
        ErrorType = ResultErrorType.None;
    }

    private Result(string error, ResultErrorType errorType)
    {
        IsSuccess = false;
        Error = error;
        ErrorType = errorType;
    }

    public static Result<T> Success(T value) => new(value);
    public static Result<T> NotFound(string error) => new(error, ResultErrorType.NotFound);
    public static Result<T> Validation(string error) => new(error, ResultErrorType.Validation);
    public static Result<T> Conflict(string error) => new(error, ResultErrorType.Conflict);
    public static Result<T> Unauthorized(string error) => new(error, ResultErrorType.Unauthorized);
    public static Result<T> Failure(string error) => new(error, ResultErrorType.Failure);
}

public enum ResultErrorType
{
    None,
    NotFound,
    Validation,
    Conflict,
    Unauthorized,
    Failure
}