namespace Core.IntegrationTests.Shared.Infrastructure;

public class TestHttpClientFactory : IHttpClientFactory
{
    public HttpClient CreateClient(string name) => new();
}