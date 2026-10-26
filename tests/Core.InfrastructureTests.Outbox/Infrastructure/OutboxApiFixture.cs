using System.Net.Http.Headers;
using Core.Caching;
using Core.CQRS.Decorators;
using Core.DataAccessTypes;
using Core.DateTimeProvider;
using Core.IntegrationTests.Shared.Fixtures;
using Core.IntegrationTests.Shared.Infrastructure;
using Core.Json;
using Core.Logger;
using Core.Outbox;
using Core.Outbox.Controllers;
using Core.Permissions;
using Core.RequestContext;
using Hangfire;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Xunit;

namespace Core.InfrastructureTests.Outbox.Infrastructure;

public class OutboxApiFixture : IAsyncLifetime
{
    public const string DeadLettersEndpoint = "/api/outbox/dead-letters";

    private readonly PostgresFixture _postgres = new();
    private WebApplication _app = null!;

    public TestDateTimeProvider DateTimeProvider { get; } = new();

    public IServiceProvider Services => _app.Services;

    public async Task InitializeAsync()
    {
        await _postgres.InitializeAsync();
        await _postgres.EnsureSchemaCreatedAsync();

        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();

        builder.Services.AddDbContext<TestDbContext>(o => o.UseNpgsql(_postgres.ConnectionString));
        builder.Services.AddSingleton<IJsonSerializer, TestJsonSerializer>();
        // AddOutbox schedules its jobs on start; no Hangfire storage in these tests
        builder.Services.AddSingleton(Substitute.For<IRecurringJobManager>());
        builder.Services.AddSingleton<IDateTimeProvider>(DateTimeProvider);
        builder.Services.AddAppLogger();
        builder.Services.AddSingleton(Substitute.For<ICacheService>());
        builder.Services.AddRequestContext();
        builder.Services.AddPermissions<TestDbContext>();
        builder.Services.AddUnitOfWork<TestDbContext>();
        builder.Services.AddOutbox<TestDbContext>(builder.Configuration);
        builder.Services.AddCqrsDecorators();
        builder.Services.AddControllers().AddApplicationPart(typeof(OutboxController).Assembly);

        builder.Services.AddAuthentication(TestAuthHandler.SchemeName)
            .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(
                TestAuthHandler.SchemeName, _ => { });
        builder.Services.AddAuthorization();

        _app = builder.Build();

        _app.UseAuthentication();
        _app.UseAuthorization();
        _app.MapControllers();

        await _app.StartAsync();
    }

    public TestDbContext CreateDbContext() => _postgres.CreateDbContext();

    public HttpClient CreateAuthenticatedClient(Guid userId)
    {
        var client = _app.GetTestClient();
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