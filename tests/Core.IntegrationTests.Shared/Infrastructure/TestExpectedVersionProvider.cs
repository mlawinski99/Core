using Core.RequestContext;

namespace Core.IntegrationTests.Shared.Infrastructure;

public class TestExpectedVersionProvider : IExpectedVersionProvider
{
    public int? ExpectedVersion { get; set; }
}