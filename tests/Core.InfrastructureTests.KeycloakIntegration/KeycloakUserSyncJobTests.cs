using Core.IntegrationTests.Shared;
using Core.IntegrationTests.Shared.Fixtures;
using Core.IntegrationTests.Shared.Infrastructure;
using Core.KeycloakSync;
using Core.Logger;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;

namespace Core.InfrastructureTests.KeycloakIntegration;

[Collection("Keycloak")]
public class KeycloakUserSyncJobTests(PostgresFixture postgresFixture, KeycloakFixture keycloakFixture)
    : IntegrationTestBase(postgresFixture)
{
    [Fact]
    public async Task Run_WithNewKeycloakUser_ImportsEventAndSyncsUserIntoDatabase()
    {
        // Arrange
        var keycloakService = keycloakFixture.CreateKeycloakService();
        var token = await keycloakService.GetToken();

        var username = $"syncjob-{Guid.NewGuid():N}";
        await keycloakService.CreateUser(token, username, $"{username}@test.com", "password123");

        await Task.Delay(500);

        var importer = new KeycloakEventImporter<TestDbContext>(
            Db,
            new TestHttpClientFactory(),
            Options.Create(keycloakFixture.CreateKeycloakConfig()),
            Substitute.For<IAppLogger<KeycloakEventImporter<TestDbContext>>>(),
            keycloakService,
            new TestJsonSerializer());

        var processor = new KeycloakEventProcessor<TestDbContext>(
            Db,
            keycloakService,
            Encryptor,
            DateTimeProvider,
            Substitute.For<IAppLogger<KeycloakEventProcessor<TestDbContext>>>());

        var job = new KeycloakUserSyncJob<TestDbContext>(importer, processor);

        // Act
        await job.Run(CancellationToken.None);

        // Assert
        var syncedUser = await Db.Users.SingleOrDefaultAsync(u => u.UserName == username);
        syncedUser.Should().NotBeNull();

        var events = await Db.KeycloakAdminEvents
            .Where(e => e.OperationType == "CREATE"
                        && e.ResourcePath.Contains(syncedUser.KeycloakId.ToString()))
            .ToListAsync();
        events.Should().NotBeEmpty();
        events.Should().OnlyContain(e => e.IsProcessed);
    }
}
