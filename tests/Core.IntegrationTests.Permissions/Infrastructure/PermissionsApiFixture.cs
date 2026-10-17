using System.Net.Http.Headers;
using Core.IntegrationTests.Shared.Fixtures;
using Core.IntegrationTests.Shared.Infrastructure;
using Core.Permissions;
using Core.RequestContext;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Core.IntegrationTests.Permissions.Infrastructure;

public class PermissionsApiFixture : IAsyncLifetime
{
    public const string ReadEndpoint = "/read";

    private readonly PostgresFixture _postgres = new();
    private WebApplication _app = null!;

    public async Task InitializeAsync()
    {
        await _postgres.InitializeAsync();
        await _postgres.EnsureSchemaCreatedAsync();

        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();

        builder.Services.AddDbContext<TestDbContext>(o => o.UseNpgsql(_postgres.ConnectionString));
        builder.Services.AddRequestContext();
        builder.Services.AddPermissions<TestDbContext>();

        builder.Services.AddAuthentication(TestAuthHandler.SchemeName)
            .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(
                TestAuthHandler.SchemeName, _ => { });
        builder.Services.AddAuthorization();

        _app = builder.Build();

        _app.UseAuthentication();
        _app.UseAuthorization();
        _app.MapGet(ReadEndpoint, () => Results.Ok())
            .RequireAuthorization(new HasPermissionAttribute(TestPermission.Read));

        await _app.StartAsync();
    }

    public TestDbContext CreateDbContext() => _postgres.CreateDbContext();

    public HttpClient CreateClient() => _app.GetTestClient();

    public HttpClient CreateAuthenticatedClient(Guid userId)
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", userId.ToString());
        return client;
    }

    public async Task DisposeAsync()
    {
        await _app.DisposeAsync();
        await _postgres.DisposeAsync();
    }
}