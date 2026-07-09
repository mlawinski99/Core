using Core.InfrastructureTests.KeycloakIntegration.Fixtures;
using Core.IntegrationTests.Shared;
using Core.IntegrationTests.Shared.Infrastructure;
using Core.Keycloak;
using Core.KeycloakSync;
using Core.Logger;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;

namespace Core.InfrastructureTests.KeycloakIntegration;

[Collection("KeycloakEventSync")]
public class KeycloakEventImporterTests : IntegrationTestBase<KeycloakEventSyncTestFixture>
{
    public KeycloakEventImporterTests(KeycloakEventSyncTestFixture fixture) : base(fixture)
    {
    }

    private KeycloakEventImporter<TestDbContext> CreateImporter(IKeycloakService keycloakService)
    {
        var logger = Substitute.For<IAppLogger<KeycloakEventImporter<TestDbContext>>>();
        return new KeycloakEventImporter<TestDbContext>(
            Db,
            new TestHttpClientFactory(),
            Options.Create(Fixture.CreateKeycloakConfig()),
            logger,
            keycloakService,
            new TestJsonSerializer());
    }

    [Fact]
    public async Task ImportEventsAsync_WithCreateUserEvent_ShouldStoreEventInDatabase()
    {
        // Arrange
        var keycloakService = Fixture.CreateKeycloakService();
        var token = await keycloakService.GetToken();

        var username = "test24";
        var password = "testPassword123";
        var email = $"{username}24@test.com";
        await keycloakService.CreateUser(token, username, email, password);

        await Task.Delay(500);

        var importer = CreateImporter(keycloakService);

        // Act
        await importer.ImportEventsAsync();

        // Assert
        var latestEvent = await Db.KeycloakAdminEvents
            .OrderByDescending(e => e.Time)
            .FirstAsync();

        latestEvent.OperationType.Should().Be("CREATE");
        latestEvent.ResourceType.Should().Be("USER");
        latestEvent.IsProcessed.Should().BeFalse();
    }

    [Fact]
    public async Task ImportEventsAsync_WithUpdateUserEvent_ShouldStoreEventInDatabase()
    {
        // Arrange
        var keycloakService = Fixture.CreateKeycloakService();
        var token = await keycloakService.GetToken();

        await keycloakService.UpdateUser(token, KeycloakTestUsersData.TestUserId, "testEmailUpdate@example.com");

        await Task.Delay(500);

        var importer = CreateImporter(keycloakService);

        // Act
        await importer.ImportEventsAsync();

        // Assert
        var latestEvent = await Db.KeycloakAdminEvents
            .OrderByDescending(e => e.Time)
            .FirstAsync();

        latestEvent.OperationType.Should().Be("UPDATE");
        latestEvent.ResourceType.Should().Be("USER");
        latestEvent.IsProcessed.Should().BeFalse();
    }

    [Fact]
    public async Task ImportEventsAsync_WithDeleteUserEvent_ShouldStoreEventInDatabase()
    {
        // Arrange
        var keycloakService = Fixture.CreateKeycloakService();
        var token = await keycloakService.GetToken();

        var username = "testcreate";
        var email = $"{username}create@test.com";
        await keycloakService.CreateUser(token, username, email, "testPassword123");

        await Task.Delay(500);

        var importer = CreateImporter(keycloakService);

        await importer.ImportEventsAsync();

        var createEvent = await Db.KeycloakAdminEvents
            .Where(e => e.OperationType == "CREATE" && e.ResourceType == "USER")
            .OrderByDescending(e => e.Time)
            .FirstAsync();

        var userId = createEvent.ResourcePath.Split('/').Last();

        await keycloakService.DeleteUser(token, userId);

        await Task.Delay(500);

        // Act
        await importer.ImportEventsAsync();

        // Assert
        var events = await Db.KeycloakAdminEvents
            .Where(e => e.ResourcePath.Contains(userId))
            .OrderBy(e => e.Time)
            .ToListAsync();

        events.Should().HaveCount(2);
        events[0].OperationType.Should().Be("CREATE");
        events[1].OperationType.Should().Be("DELETE");
    }

    [Fact]
    public async Task ImportEventsAsync_WithNoNewEvents_ShouldNotInsertDuplicates()
    {
        // Arrange
        var keycloakService = Fixture.CreateKeycloakService();
        var token = await keycloakService.GetToken();

        var username = "test";
        var password = "testPassword123";
        var email = $"{username}@test.com";
        await keycloakService.CreateUser(token, username, email, password);

        await Task.Delay(500);

        var importer = CreateImporter(keycloakService);

        await importer.ImportEventsAsync();

        var eventsAfterFirstSync = await Db.KeycloakAdminEvents.CountAsync();

        // Act
        await importer.ImportEventsAsync();

        // Assert
        var eventsAfterSecondSync = await Db.KeycloakAdminEvents.CountAsync();

        eventsAfterSecondSync.Should().Be(eventsAfterFirstSync);
    }
}