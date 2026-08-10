using System.Net;
using Core.IntegrationTests.Shared.Fixtures;
using Core.Keycloak;
using Core.IntegrationTests.Shared.Infrastructure;
using FluentAssertions;
using Xunit;

namespace Core.InfrastructureTests.KeycloakIntegration;

[Collection("Keycloak")]
public class KeycloakServiceTests(KeycloakFixture keycloakFixture)
{
    [Fact]
    public async Task GetToken_ShouldReturnValidToken()
    {
        // Arrange
        var keycloakService = keycloakFixture.CreateKeycloakService();

        // Act
        var token = await keycloakService.GetToken();

        // Assert
        token.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task GetUser_WithExistingUser_ShouldReturnUser()
    {
        // Arrange
        var keycloakService = keycloakFixture.CreateKeycloakService();
        var token = await keycloakService.GetToken();

        // Act
        var user = await keycloakService.GetUser(token, KeycloakTestUsersData.TestUserId);

        // Assert
        user.Should().NotBeNull();
        user.Username.Should().Be(KeycloakTestUsersData.TestUsername);
    }

    [Fact]
    public async Task GetUser_WithNonExistingUser_ShouldReturnNull()
    {
        // Arrange
        var keycloakService = keycloakFixture.CreateKeycloakService();
        var token = await keycloakService.GetToken();

        // Act
        var user = await keycloakService.GetUser(token, Guid.NewGuid().ToString());

        // Assert
        user.Should().BeNull();
    }

    [Fact]
    public async Task CreateUser_ShouldCreateUserThatCanAuthenticate()
    {
        // Arrange
        var keycloakService = keycloakFixture.CreateKeycloakService();
        var token = await keycloakService.GetToken();
        var username = $"testUserLogin-{Guid.NewGuid()}";

        // Act
        await keycloakService.CreateUser(token, username, $"{username}@test.com", "testPassword123");

        // Assert
        var login = await keycloakService.LoginUser(username, "testPassword123");
        login.Should().NotBeNull();
        login.AccessToken.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task CreateUser_WhenUserAlreadyExists_ShouldThrowConflict()
    {
        // Arrange
        var keycloakService = keycloakFixture.CreateKeycloakService();
        var token = await keycloakService.GetToken();
        var username = $"testUserCreate-{Guid.NewGuid()}";
        await keycloakService.CreateUser(token, username, $"{username}@test.com", "testPassword123");

        // Act
        var act = () => keycloakService.CreateUser(token, username, $"{username}@test.com", "testPassword123");

        // Assert
        var exception = await act.Should().ThrowAsync<KeycloakException>();
        exception.Which.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task UpdateUser_ShouldUpdateUserEmail()
    {
        // Arrange
        var keycloakService = keycloakFixture.CreateKeycloakService();
        var token = await keycloakService.GetToken();

        // Act
        await keycloakService.UpdateUser(token, KeycloakTestUsersData.TestUserUpdateId, "updatedEmail@test.com");

        // Assert
        var user = await keycloakService.GetUser(token, KeycloakTestUsersData.TestUserUpdateId);
        user.Should().NotBeNull();
        user.Email.Should().Be("updatedemail@test.com");
    }

    [Fact]
    public async Task DeleteUser_ShouldDeleteUserFromKeycloak()
    {
        // Arrange
        var keycloakService = keycloakFixture.CreateKeycloakService();
        var token = await keycloakService.GetToken();
        var userIdToDelete = KeycloakTestUsersData.TestUserDeleteId;

        // Act
        var act = () => keycloakService.DeleteUser(token, userIdToDelete);

        // Assert
        await act.Should().NotThrowAsync();

        var deletedUser = await keycloakService.GetUser(token, userIdToDelete);
        deletedUser.Should().BeNull();
    }
}
