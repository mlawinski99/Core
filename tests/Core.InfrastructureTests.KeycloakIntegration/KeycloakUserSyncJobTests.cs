using Core.InfrastructureTests.KeycloakIntegration.Fixtures;
using Core.IntegrationTests.Shared;
using Core.IntegrationTests.Shared.Infrastructure;
using Core.KeycloakSync;
using Core.Logger;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;

namespace Core.InfrastructureTests.KeycloakIntegration;

[Collection("KeycloakEventSync")]
public class KeycloakUserSyncJobTests : IntegrationTestBase<KeycloakEventSyncTestFixture>
{
    public KeycloakUserSyncJobTests(KeycloakEventSyncTestFixture fixture) : base(fixture)
    {
    }

    [Fact]
    public async Task Run_WithNewKeycloakUser_ImportsEventAndSyncsUserIntoDatabase()
    {
        // Arrange
        var keycloakService = Fixture.CreateKeycloakService();
        var token = await keycloakService.GetToken();

        var username = $"syncjob-{Guid.NewGuid():N}";
        await keycloakService.CreateUser(token, username, $"{username}@test.com", "password123");

        await Task.Delay(500);

        var importer = new KeycloakEventImporter<TestDbContext>(
            Db,
            new TestHttpClientFactory(),
            Options.Create(Fixture.CreateKeycloakConfig()),
            Substitute.For<IAppLogger<KeycloakEventImporter<TestDbContext>>>(),
            keycloakService,
            new TestJsonSerializer());

        var processor = new KeycloakEventProcessor<TestDbContext>(
            Db,
            keycloakService,
            Substitute.For<IAppLogger<KeycloakEventProcessor<TestDbContext>>>());

        var job = new KeycloakUserSyncJob<TestDbContext>(importer, processor);

        // Act
        await job.Run(CancellationToken.None);

        // Assert
        var users = await Db.Users.ToListAsync();
        users.Should().Contain(u => u.UserName == username);

        var events = await Db.KeycloakAdminEvents
            .Where(e => e.OperationType == "CREATE")
            .ToListAsync();
        events.Should().NotBeEmpty();
        events.Should().OnlyContain(e => e.IsProcessed);
    }
}