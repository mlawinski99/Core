using System.Net.Http.Headers;
using Core.CQRS;
using Core.CQRS.Decorators;
using Core.Identity.Users.Commands;
using Core.Identity.Web;
using Core.Infrastructure;
using Core.IntegrationTests.Shared.Infrastructure;
using Core.Keycloak;
using Core.Logger;
using Core.Validation;
using FluentValidation;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Xunit;

namespace Core.IntegrationTests.Identity.Infrastructure;

public class UsersApiFixture : IAsyncLifetime
{
    private WebApplication _app = null!;

    public IKeycloakService KeycloakService { get; } = Substitute.For<IKeycloakService>();

    public async Task InitializeAsync()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();

        builder.Services
            .AddControllers()
            .AddApplicationPart(typeof(UsersController).Assembly);

        builder.Services.AddCqrs(typeof(RegisterUser).Assembly);
        builder.Services.AddValidation();
        builder.Services.AddValidatorsFromAssembly(typeof(RegisterUser).Assembly);
        builder.Services.AddAppLogger();
        builder.Services.AddHttpContextAccessor();
        builder.Services.AddScoped<IUserProvider, UserProvider>();
        builder.Services.AddSingleton(KeycloakService);
        builder.Services.AddCqrsDecorators();

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

    public HttpClient CreateClient() => _app.GetTestClient();

    public HttpClient CreateAuthenticatedClient(string userId = KeycloakTestUsersData.TestUserId)
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", userId);
        return client;
    }

    public async Task DisposeAsync() => await _app.DisposeAsync();
}
