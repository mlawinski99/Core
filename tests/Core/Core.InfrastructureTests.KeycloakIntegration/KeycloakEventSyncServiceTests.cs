using Core.Infrastructure.Json;
using Core.InfrastructureTests.KeycloakIntegration.Fixtures;
using Chatter.IntegrationTests.Shared;
using Chatter.IntegrationTests.Shared.Infrastructure;
using Core.Logger;
using Chatter.SyncUsersJob;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;

namespace Core.InfrastructureTests.KeycloakIntegration;

[Collection("KeycloakEventSync")]
public class KeycloakEventSyncServiceTests : IntegrationTestBase<KeycloakEventSyncTestFixture>
{
    public KeycloakEventSyncServiceTests(KeycloakEventSyncTestFixture fixture) : base(fixture)
    {
    }

    [Fact]
    public async Task SyncUserEventsAsync_WithCreateUserEvent_ShouldStoreEventInDatabase()
    {
        // Arrange
        var keycloakService = Fixture.CreateKeycloakService();
        var token = await keycloakService.GetToken();

        var username = "test24";
        var password = "testPassword123";
        var email = $"{username}24@test.com";
        await keycloakService.CreateUser(token, username, email, password);

        await Task.Delay(500);

        var config = Fixture.CreateKeycloakConfig();
        var logger = Substitute.For<IAppLogger<KeycloakEventSyncService>>();
        var syncService = new KeycloakEventSyncService(
            new TestHttpClientFactory(),
            Options.Create(config),
            logger,
            keycloakService,
            new TestJsonSerializer());

        // Act
        await syncService.SyncUserEventsAsync();

        // Assert
        var events = await Db.KeycloakAdminEvents
            .Where(e => e.OperationType == "CREATE" && e.ResourceType == "USER")
            .ToListAsync();

        events.Should().NotBeEmpty();
        events.Should().OnlyContain(e => e.IsProcessed == false);
    }

    [Fact]
    public async Task SyncUserEventsAsync_WithUpdateUserEvent_ShouldStoreEventInDatabase()
    {
        // Arrange
        var keycloakService = Fixture.CreateKeycloakService();
        var token = await keycloakService.GetToken();

        await keycloakService.UpdateUser(token, KeycloakTestUsersData.TestUserId, "testEmailUpdate@example.com");

        await Task.Delay(500);

        var config = Fixture.CreateKeycloakConfig();
        var logger = Substitute.For<IAppLogger<KeycloakEventSyncService>>();
        var syncService = new KeycloakEventSyncService(
            new TestHttpClientFactory(),
            Options.Create(config),
            logger,
            keycloakService,
            new TestJsonSerializer());

        // Act
        await syncService.SyncUserEventsAsync();

        // Assert
        var events = await Db.KeycloakAdminEvents
            .Where(e => e.OperationType == "UPDATE" && e.ResourceType == "USER")
            .ToListAsync();

        events.Should().NotBeEmpty();
        events.Should().OnlyContain(e => e.IsProcessed == false);
    }

    [Fact]
    public async Task SyncUserEventsAsync_WithDeleteUserEvent_ShouldStoreEventInDatabase()
    {
        // Arrange
        var keycloakService = Fixture.CreateKeycloakService();
        var token = await keycloakService.GetToken();

        var username = "testcreate";
        var email = $"{username}create@test.com";
        await keycloakService.CreateUser(token, username, email, "testPassword123");

        await Task.Delay(500);

        var config = Fixture.CreateKeycloakConfig();
        var logger = Substitute.For<IAppLogger<KeycloakEventSyncService>>();
        var syncService = new KeycloakEventSyncService(
            new TestHttpClientFactory(),
            Options.Create(config),
            logger,
            keycloakService,
            new TestJsonSerializer());

        await syncService.SyncUserEventsAsync();

        var createEvent = await Db.KeycloakAdminEvents
            .Where(e => e.OperationType == "CREATE" && e.ResourceType == "USER")
            .OrderByDescending(e => e.Time)
            .FirstAsync();

        var userId = createEvent.ResourcePath.Split('/').Last();

        await keycloakService.DeleteUser(token, userId);

        await Task.Delay(500);

        // Act
        await syncService.SyncUserEventsAsync();

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
    public async Task SyncUserEventsAsync_WithNoNewEvents_ShouldNotInsertDuplicates()
    {
        // Arrange
        var keycloakService = Fixture.CreateKeycloakService();
        var token = await keycloakService.GetToken();

        var username = "test";
        var password = "testPassword123";
        var email = $"{username}@test.com";
        await keycloakService.CreateUser(token, username, email, password);

        await Task.Delay(500);

        var config = Fixture.CreateKeycloakConfig();
        var logger = Substitute.For<IAppLogger<KeycloakEventSyncService>>();
        var syncService = new KeycloakEventSyncService(
            new TestHttpClientFactory(),
            Options.Create(config),
            logger,
            keycloakService,
            new TestJsonSerializer());

        await syncService.SyncUserEventsAsync();

        var eventsAfterFirstSync = await Db.KeycloakAdminEvents.CountAsync();

        // Act
        await syncService.SyncUserEventsAsync();

        // Assert
        var eventsAfterSecondSync = await Db.KeycloakAdminEvents.CountAsync();

        eventsAfterSecondSync.Should().Be(eventsAfterFirstSync);
    }
}