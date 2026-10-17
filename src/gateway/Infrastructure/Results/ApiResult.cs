using Microsoft.AspNetCore.Http;

namespace Gateway.Infrastructure.Results;

public abstract class ApiResult
{
    public int StatusCode { get; protected init; }
    public object? Value { get; protected init; }

    protected ApiResult(int statusCode, object? value = null)
    {
        StatusCode = statusCode;
        Value = value;
    }

    public static ApiResult BadRequest(string error) => new ErrorResult(StatusCodes.Status400BadRequest, new { error });
    public static ApiResult NotFound(string error) => new ErrorResult(StatusCodes.Status404NotFound, new { error });
    public static ApiResult Conflict(string error) => new ErrorResult(StatusCodes.Status409Conflict, new { error});
    public static ApiResult Forbidden() => new ErrorResult(StatusCodes.Status403Forbidden, null);
    public static ApiResult NoContent() => new EmptyResult(StatusCodes.Status204NoContent);

    public abstract IResult ToResult();

    private sealed class ErrorResult : ApiResult
    {
        public ErrorResult(int statusCode, object? value) : base(statusCode, value) { }
        public override IResult ToResult() => Microsoft.AspNetCore.Http.Results.Json(Value, statusCode: StatusCode);
    }

    private sealed class EmptyResult : ApiResult
    {
        public EmptyResult(int statusCode) : base(statusCode, null) { }
        public override IResult ToResult() => Microsoft.AspNetCore.Http.Results.NoContent();
    }
}

public sealed class ApiResult<T> : ApiResult
{
    private ApiResult(int statusCode, T? value, string? errorMessage = null) : base(statusCode, value)
    {
        Value = value;
        ErrorMessage = errorMessage;
    }

    public string? ErrorMessage { get; init; }
    public bool IsSuccess => ErrorMessage is null && StatusCode < 400;

    public static ApiResult<T> Ok(T value) => new(StatusCodes.Status200OK, value);
    public static ApiResult<T> Created(T value) => new(StatusCodes.Status201Created, value);
    public static ApiResult<T> Error(int statusCode, string errorMessage) => new(statusCode, default, errorMessage);
    public static new ApiResult<T> NoContent() => new(StatusCodes.Status204NoContent, default);

    public override IResult ToResult()
    {
        return StatusCode switch
        {
            StatusCodes.Status204NoContent => Microsoft.AspNetCore.Http.Results.NoContent(),
            _ when IsSuccess => Microsoft.AspNetCore.Http.Results.Json(Value, statusCode: StatusCode),
            _ => Microsoft.AspNetCore.Http.Results.Json(new { error = ErrorMessage }, statusCode: StatusCode)
        };
    }
}
