using System.Net;
using System.Net.Http.Json;
using Core.Identity.Users.Errors;
using Core.IntegrationTests.Identity.Infrastructure;
using Core.IntegrationTests.Shared.Infrastructure;
using Core.Keycloak;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace Core.IntegrationTests.Identity;

[Collection("UsersApi")]
public class LoginUserTests
{
    private readonly HttpClient _client;
    private readonly UsersApiFixture _fixture;

    public LoginUserTests(UsersApiFixture fixture)
    {
        _fixture = fixture;
        _client = fixture.CreateClient();
    }

    [Fact]
    public async Task LoginUser_ValidCredentials_Returns200WithTokens()
    {
        _fixture.KeycloakService.LoginUser(KeycloakTestUsersData.TestUsername, KeycloakTestUsersData.TestPassword)
            .Returns(new KeycloakTokenResponse
            {
                AccessToken = "fake-access-token",
                RefreshToken = "fake-refresh-token",
                ExpiresIn = 300,
                RefreshExpiresIn = 1800,
                TokenType = "Bearer"
            });

        var response = await _client.PostAsJsonAsync("/api/users/login", new
        {
            Username = KeycloakTestUsersData.TestUsername,
            Password = KeycloakTestUsersData.TestPassword
        });

        var result = await response.ReadResult<KeycloakTokenResponse>();
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        result.IsSuccess.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data!.AccessToken.Should().Be("fake-access-token");
        result.Data.RefreshToken.Should().Be("fake-refresh-token");
    }

    [Fact]
    public async Task LoginUser_InvalidPassword_Returns401()
    {
        _fixture.KeycloakService.LoginUser(KeycloakTestUsersData.TestUsername, "wrong-password")
            .Returns((KeycloakTokenResponse?)null);

        var response = await _client.PostAsJsonAsync("/api/users/login", new
        {
            Username = KeycloakTestUsersData.TestUsername,
            Password = "wrong-password"
        });

        var result = await response.ReadResult();
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain(ErrorMessages.InvalidUsernameOrPassword);
    }

    [Fact]
    public async Task LoginUser_NonExistentUser_Returns401()
    {
        _fixture.KeycloakService.LoginUser("nonexistent", "password123")
            .Returns((KeycloakTokenResponse?)null);

        var response = await _client.PostAsJsonAsync("/api/users/login", new
        {
            Username = "nonexistent",
            Password = "password123"
        });

        var result = await response.ReadResult();
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task LoginUser_EmptyUsername_Returns400()
    {
        var response = await _client.PostAsJsonAsync("/api/users/login", new
        {
            Username = "",
            Password = "password123"
        });

        var result = await response.ReadResult();
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain(ValidationMessages.UsernameRequired);
    }

    [Fact]
    public async Task LoginUser_EmptyPassword_Returns400()
    {
        var response = await _client.PostAsJsonAsync("/api/users/login", new
        {
            Username = "user",
            Password = ""
        });

        var result = await response.ReadResult();
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain(ValidationMessages.PasswordRequired);
    }
}
