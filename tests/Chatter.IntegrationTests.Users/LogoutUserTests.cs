using System.Net;
using System.Net.Http.Json;
using Chatter.IntegrationTests.Shared.Infrastructure;
using Chatter.IntegrationTests.Users.Infrastructure;
using Chatter.Users.Application.Users.Errors;
using Core.KeycloakService;
using FluentAssertions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Xunit;

namespace Chatter.IntegrationTests.Users;

[Collection("UsersApi")]
public class LogoutUserTests
{
    private readonly UsersTestFixture _fixture;

    public LogoutUserTests(UsersTestFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task LogoutUser_ValidRefreshToken_Returns200()
    {
        var client = _fixture.Api.CreateAuthenticatedClient();

        _fixture.KeycloakService.LogoutUser("valid-refresh-token")
            .Returns(Task.CompletedTask);

        var response = await client.PostAsJsonAsync("/api/users/logout", new
        {
            RefreshToken = "valid-refresh-token"
        });

        var result = await response.ReadResult();
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task LogoutUser_EmptyRefreshToken_Returns400()
    {
        var client = _fixture.Api.CreateAuthenticatedClient();

        var response = await client.PostAsJsonAsync("/api/users/logout", new
        {
            RefreshToken = ""
        });

        var result = await response.ReadResult();
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain(ValidationMessages.RefreshTokenRequired);
    }

    [Fact]
    public async Task LogoutUser_KeycloakFails_Returns400()
    {
        var client = _fixture.Api.CreateAuthenticatedClient();

        _fixture.KeycloakService.LogoutUser("token")
            .Throws(new KeycloakException("Keycloak LogoutUser failed with code BadRequest", HttpStatusCode.BadRequest));

        var response = await client.PostAsJsonAsync("/api/users/logout", new
        {
            RefreshToken = "token"
        });

        var result = await response.ReadResult();
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain(ErrorMessages.FailedToLogout);
    }

    [Fact]
    public async Task LogoutUser_Unauthenticated_Returns401()
    {
        var client = _fixture.Api.CreateClient();

        var response = await client.PostAsJsonAsync("/api/users/logout", new
        {
            RefreshToken = "token"
        });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}