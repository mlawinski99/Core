namespace Core.Infrastructure;

public interface IExpectedVersionProvider
{
    int? ExpectedVersion { get; }
}