using Core.IntegrationTests.Shared;
using Core.IntegrationTests.Shared.Fixtures;
using Core.IntegrationTests.Shared.Infrastructure;
using Core.Identity.Domain;
using Core.Logger;
using Core.Keycloak;
using Core.KeycloakSync;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Xunit;

namespace Core.InfrastructureTests.KeycloakIntegration;

[Collection("KeycloakIntegration")]
public class KeycloakEventProcessorTests : IntegrationTestBase<KeycloakIntegrationTestFixture>
{
    public KeycloakEventProcessorTests(KeycloakIntegrationTestFixture fixture) : base(fixture)
    {
    }

    [Fact]
    public async Task KeycloakEventProcessor_WithCreateUserEvent_ShouldSyncUserFromKeycloak()
    {
        // Arrange
        var keycloakEvent = new KeycloakAdminEvent
        {
            OperationType = "CREATE",
            ResourceType = "USER",
            ResourcePath = $"users/{KeycloakTestUsersData.TestUserId}",
            Time = Fixture.DateTimeProvider.UtcNow,
            IsProcessed = false
        };

        Db.KeycloakAdminEvents.Add(keycloakEvent);
        await Db.SaveChangesAsync();

        var keycloakService = Fixture.CreateKeycloakService();
        var logger = Substitute.For<IAppLogger<KeycloakEventProcessor<TestDbContext>>>();
        var processor = new KeycloakEventProcessor<TestDbContext>(Db, keycloakService, logger);

        // Act
        await processor.Run();

        // Assert
        var user = await Db.Users.FirstOrDefaultAsync(u => u.KeycloakId == Guid.Parse(KeycloakTestUsersData.TestUserId));
        user.Should().NotBeNull();
        user.UserName.Should().Be(KeycloakTestUsersData.TestUsername);
        user.Email.Should().Be(KeycloakTestUsersData.TestEmail);

        var processedEvent = await Db.KeycloakAdminEvents.FirstAsync();
        processedEvent.IsProcessed.Should().BeTrue();
    }

    [Fact]
    public async Task KeycloakEventProcessor_WithUpdateUserEvent_ShouldUpdateExistingUser()
    {
        // Arrange
        var existingUser = new User
        {
            Id = Guid.NewGuid(),
            KeycloakId = Guid.Parse(KeycloakTestUsersData.TestUserId),
            UserName = "oldusername",
            Email = "old@test.com"
        };
        Db.Users.Add(existingUser);

        var keycloakEvent = new KeycloakAdminEvent
        {
            OperationType = "UPDATE",
            ResourceType = "USER",
            ResourcePath = $"users/{KeycloakTestUsersData.TestUserId}",
            Time = Fixture.DateTimeProvider.UtcNow,
            IsProcessed = false
        };
        Db.KeycloakAdminEvents.Add(keycloakEvent);
        await Db.SaveChangesAsync();

        var keycloakService = Fixture.CreateKeycloakService();
        var logger = Substitute.For<IAppLogger<KeycloakEventProcessor<TestDbContext>>>();
        var processor = new KeycloakEventProcessor<TestDbContext>(Db, keycloakService, logger);

        // Act
        await processor.Run();

        // Assert
        var user = await Db.Users.FirstOrDefaultAsync(u => u.KeycloakId == Guid.Parse(KeycloakTestUsersData.TestUserId));
        user.Should().NotBeNull();
        user.UserName.Should().Be(KeycloakTestUsersData.TestUsername);
        user.Email.Should().Be(KeycloakTestUsersData.TestEmail);
    }

    [Fact]
    public async Task KeycloakEventProcessor_WithDeleteUserEvent_ShouldDeleteUser()
    {
        // Arrange
        var existingUser = new User
        {
            Id = Guid.NewGuid(),
            KeycloakId = Guid.Parse(KeycloakTestUsersData.TestUserId),
            UserName = KeycloakTestUsersData.TestUsername,
            Email = KeycloakTestUsersData.TestEmail
        };
        Db.Users.Add(existingUser);

        var keycloakEvent = new KeycloakAdminEvent
        {
            OperationType = "DELETE",
            ResourceType = "USER",
            ResourcePath = $"users/{KeycloakTestUsersData.TestUserId}",
            Time = Fixture.DateTimeProvider.UtcNow,
            IsProcessed = false
        };
        Db.KeycloakAdminEvents.Add(keycloakEvent);
        await Db.SaveChangesAsync();

        var keycloakService = Fixture.CreateKeycloakService();
        var logger = Substitute.For<IAppLogger<KeycloakEventProcessor<TestDbContext>>>();
        var processor = new KeycloakEventProcessor<TestDbContext>(Db, keycloakService, logger);

        // Act
        await processor.Run();

        // Assert
        var user = await Db.Users.FirstOrDefaultAsync(u => u.KeycloakId == Guid.Parse(KeycloakTestUsersData.TestUserId));
        user.Should().BeNull();

        var processedEvent = await Db.KeycloakAdminEvents.FirstAsync();
        processedEvent.IsProcessed.Should().BeTrue();
    }

    [Fact]
    public async Task KeycloakEventProcessor_WithNoEvents_ShouldNotThrow()
    {
        // Arrange
        var keycloakService = Fixture.CreateKeycloakService();
        var logger = Substitute.For<IAppLogger<KeycloakEventProcessor<TestDbContext>>>();
        var processor = new KeycloakEventProcessor<TestDbContext>(Db, keycloakService, logger);

        // Act
        var act = () => processor.Run();

        // Assert
        await act.Should().NotThrowAsync();
    }
}