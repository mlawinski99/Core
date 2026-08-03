namespace Core.ResultPattern;

public interface IResultState
{
    bool IsSuccess { get; }
    string? Error { get; }
    ResultCode Code { get; }
}