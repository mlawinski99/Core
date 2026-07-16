using System.Net;
using System.Net.Http.Json;
using Core.Identity.Users.Errors;
using Core.IntegrationTests.Identity.Infrastructure;
using Core.IntegrationTests.Shared.Infrastructure;
using Core.Keycloak;
using FluentAssertions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Xunit;

namespace Core.IntegrationTests.Identity;

[Collection("UsersApi")]
public class RegisterUserTests
{
    private readonly HttpClient _client;
    private readonly UsersApiFixture _fixture;

    public RegisterUserTests(UsersApiFixture fixture)
    {
        _fixture = fixture;
        _client = fixture.CreateClient();
    }

    [Fact]
    public async Task RegisterUser_ValidRequest_Returns200()
    {
        _fixture.KeycloakService.GetToken().Returns("token");
        _fixture.KeycloakService.CreateUser("token", "newuser", "newuser@test.com", "password123")
            .Returns(Task.CompletedTask);

        var response = await _client.PostAsJsonAsync("/api/users/register", new
        {
            Username = "newuser",
            Password = "password123",
            ConfirmPassword = "password123",
            Email = "newuser@test.com"
        });

        var result = await response.ReadResult();
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task RegisterUser_EmptyUsername_Returns400WithValidationError()
    {
        var response = await _client.PostAsJsonAsync("/api/users/register", new
        {
            Username = "",
            Password = "password123",
            ConfirmPassword = "password123",
            Email = "test@test.com"
        });

        var result = await response.ReadResult();
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain(ValidationMessages.UsernameRequired);
    }

    [Fact]
    public async Task RegisterUser_InvalidEmail_Returns400WithValidationError()
    {
        var response = await _client.PostAsJsonAsync("/api/users/register", new
        {
            Username = "user",
            Password = "password123",
            ConfirmPassword = "password123",
            Email = "invalid-email"
        });

        var result = await response.ReadResult();
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain(ValidationMessages.InvalidEmailFormat);
    }

    [Fact]
    public async Task RegisterUser_PasswordTooShort_Returns400WithValidationError()
    {
        var response = await _client.PostAsJsonAsync("/api/users/register", new
        {
            Username = "user",
            Password = "pass",
            ConfirmPassword = "pass",
            Email = "test@test.com"
        });

        var result = await response.ReadResult();
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain(ValidationMessages.PasswordMinLength);
    }

    [Fact]
    public async Task RegisterUser_PasswordsDoNotMatch_Returns400WithValidationError()
    {
        var response = await _client.PostAsJsonAsync("/api/users/register", new
        {
            Username = "user",
            Password = "password123",
            ConfirmPassword = "anotherpassword123",
            Email = "test@test.com"
        });

        var result = await response.ReadResult();
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain(ValidationMessages.PasswordsDoNotMatch);
    }

    [Fact]
    public async Task RegisterUser_DuplicateUser_Returns409()
    {
        _fixture.KeycloakService.GetToken().Returns("token");
        _fixture.KeycloakService.CreateUser("token", "existinguser", "existing@test.com", "password123")
            .Throws(new KeycloakException("Keycloak CreateUser failed with code Conflict", HttpStatusCode.Conflict));

        var response = await _client.PostAsJsonAsync("/api/users/register", new
        {
            Username = "existinguser",
            Password = "password123",
            ConfirmPassword = "password123",
            Email = "existing@test.com"
        });

        var result = await response.ReadResult();
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain(ErrorMessages.UserAlreadyExists);
    }
}
