using Core.Identity.Domain;
using Core.IntegrationTests.Shared;
using Core.IntegrationTests.Shared.Fixtures;
using Core.IntegrationTests.Shared.Infrastructure;
using Core.KeycloakSync;
using Core.Logger;
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
        var processor = new KeycloakEventProcessor<TestDbContext>(Db, keycloakService, Fixture.Encryptor,
            Fixture.DateTimeProvider, logger);

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
        var processor = new KeycloakEventProcessor<TestDbContext>(Db, keycloakService, Fixture.Encryptor,
            Fixture.DateTimeProvider, logger);

        // Act
        await processor.Run();

        // Assert
        var user = await Db.Users.FirstOrDefaultAsync(u => u.KeycloakId == Guid.Parse(KeycloakTestUsersData.TestUserId));
        user.Should().NotBeNull();
        user.UserName.Should().Be(KeycloakTestUsersData.TestUsername);
        user.Email.Should().Be(KeycloakTestUsersData.TestEmail);
    }

    [Fact]
    public async Task KeycloakEventProcessor_WithDeleteUserEvent_ShouldSoftDeleteAndAnonymizeUser()
    {
        // Arrange
        var anonymizedValue = "User Deleted";

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
        var processor = new KeycloakEventProcessor<TestDbContext>(Db, keycloakService, Fixture.Encryptor,
            Fixture.DateTimeProvider, logger);

        // Act
        await processor.Run();

        // Assert
        var user = await Db.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.KeycloakId == Guid.Parse(KeycloakTestUsersData.TestUserId));
        user.Should().BeNull();

        var deletedUser = await Db.Users
            .IgnoreQueryFilters()
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.KeycloakId == Guid.Parse(KeycloakTestUsersData.TestUserId));
        deletedUser.Should().NotBeNull();
        deletedUser.IsDeleted.Should().BeTrue();
        deletedUser.DateDeletedUtc.Should().Be(Fixture.DateTimeProvider.UtcNow);
        deletedUser.DateModifiedUtc.Should().Be(Fixture.DateTimeProvider.UtcNow);
        deletedUser.UserName.Should().Be(Fixture.Encryptor.Encrypt(anonymizedValue));
        deletedUser.Email.Should().Be(Fixture.Encryptor.Encrypt(anonymizedValue));

        var processedEvent = await Db.KeycloakAdminEvents.FirstAsync();
        processedEvent.IsProcessed.Should().BeTrue();
    }

    [Fact]
    public async Task KeycloakEventProcessor_WithNoEvents_ShouldNotThrow()
    {
        // Arrange
        var keycloakService = Fixture.CreateKeycloakService();
        var logger = Substitute.For<IAppLogger<KeycloakEventProcessor<TestDbContext>>>();
        var processor = new KeycloakEventProcessor<TestDbContext>(Db, keycloakService, Fixture.Encryptor,
            Fixture.DateTimeProvider, logger);

        // Act
        var act = () => processor.Run();

        // Assert
        await act.Should().NotThrowAsync();
    }
}
