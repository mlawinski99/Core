namespace Core.ResultPattern;

public class Result : IResult<Result>
{
    public bool IsSuccess { get; protected set; }
    public string? Error { get; protected set; }
    public ResultCode Code { get; protected set; }

    public static Result Success => new() { IsSuccess = true, Code = ResultCode.Ok };

    public static Result BadRequest(string error) => new() { IsSuccess = false, Error = error, Code = ResultCode.BadRequest };
    public static Result Unauthorized(string error) => new() { IsSuccess = false, Error = error, Code = ResultCode.Unauthorized };
    public static Result Forbidden(string error) => new() { IsSuccess = false, Error = error, Code = ResultCode.Forbidden };
    public static Result NotFound(string error) => new() { IsSuccess = false, Error = error, Code = ResultCode.NotFound };
    public static Result Conflict(string error) => new() { IsSuccess = false, Error = error, Code = ResultCode.Conflict };
    public static Result UnprocessableEntity(string error) => new() { IsSuccess = false, Error = error, Code = ResultCode.UnprocessableEntity };
    public static Result InternalError() => new() { IsSuccess = false, Error = "Something went wrong", Code = ResultCode.InternalError };
}

public class Result<T> : Result, IResult<Result<T>>
{
    public T? Data { get; private set; }

    private Result(bool isSuccess, T? data, string? error, ResultCode code)
    {
        IsSuccess = isSuccess;
        Data = data;
        Error = error;
        Code = code;
    }

    public new static Result<T> Success(T? data = default) => new(true, data, null, ResultCode.Ok);
    public new static Result<T> BadRequest(string error) => new(false, default, error, ResultCode.BadRequest);
    public new static Result<T> Unauthorized(string error) => new(false, default, error, ResultCode.Unauthorized);
    public new static Result<T> Forbidden(string error) => new(false, default, error, ResultCode.Forbidden);
    public new static Result<T> NotFound(string error) => new(false, default, error, ResultCode.NotFound);
    public new static Result<T> Conflict(string error) => new(false, default, error, ResultCode.Conflict);
    public new static Result<T> UnprocessableEntity(string error) => new(false, default, error, ResultCode.UnprocessableEntity);
    public new static Result<T> InternalError() => new(false, default, "Something went wrong", ResultCode.InternalError);
}