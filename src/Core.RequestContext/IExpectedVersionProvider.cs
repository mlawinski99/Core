namespace Core.RequestContext;

public interface IExpectedVersionProvider
{
    int? ExpectedVersion { get; }
}