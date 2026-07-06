namespace Core.ResultPattern;

public interface IResult<T> where T : IResult<T>
{
    static abstract T BadRequest(string error);
    static abstract T Unauthorized(string error);
    static abstract T Forbidden(string error);
    static abstract T NotFound(string error);
    static abstract T Conflict(string error);
    static abstract T UnprocessableEntity(string error);
    static abstract T InternalError();
}