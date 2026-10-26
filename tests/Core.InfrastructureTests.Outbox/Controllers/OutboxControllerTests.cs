using System.Net;
using Core.InfrastructureTests.Outbox.Infrastructure;
using Core.Permissions.Entities;
using FluentAssertions;
using Xunit;

namespace Core.InfrastructureTests.Outbox.Controllers;

[Collection("OutboxApi")]
public class OutboxControllerTests(OutboxApiFixture fixture)
{
    private const string ManageKey = "Core.Outbox.OutboxPermission.Manage";

    [Fact]
    public async Task GetDeadLetters_WithoutManagePermission_ShouldReturn403()
    {
        // Act
        var response = await fixture.CreateAuthenticatedClient(Guid.NewGuid()).GetAsync(OutboxApiFixture.DeadLettersEndpoint);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task ReplayDeadLetter_WithoutManagePermission_ShouldReturn403()
    {
        // Act
        var response = await fixture.CreateAuthenticatedClient(Guid.NewGuid())
            .PostAsync($"{OutboxApiFixture.DeadLettersEndpoint}/{Guid.NewGuid()}/replay", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task DiscardDeadLetter_WithoutManagePermission_ShouldReturn403()
    {
        // Act
        var response = await fixture.CreateAuthenticatedClient(Guid.NewGuid())
            .PostAsync($"{OutboxApiFixture.DeadLettersEndpoint}/{Guid.NewGuid()}/discard", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetDeadLetters_WithManagePermission_ShouldReturn200()
    {
        // Arrange
        var userId = Guid.NewGuid();
        await using var db = fixture.CreateDbContext();
        db.UserPermissions.Add(new UserPermission { UserId = userId, Permission = ManageKey });
        await db.SaveChangesAsync();

        // Act
        var response = await fixture.CreateAuthenticatedClient(userId).GetAsync(OutboxApiFixture.DeadLettersEndpoint);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}