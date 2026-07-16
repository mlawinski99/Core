using Core.DataAccessTypes;
using Core.Infrastructure;
using Core.IntegrationTests.Shared;
using Core.IntegrationTests.Shared.Fixtures;
using Core.IntegrationTests.Shared.Infrastructure;
using Core.IntegrationTests.Shared.Infrastructure.TestEntities;
using FluentAssertions;
using Xunit;

namespace Core.InfrastructureTests.DataAccessTypes;

[Collection("DataAccessTypesTest")]
public class AuditableInterceptorTests : IntegrationTestBase<IntegrationTestFixture>
{
    public AuditableInterceptorTests(IntegrationTestFixture fixture) : base(fixture)
    {
    }

    protected override TestDbContext CreateDbContext() =>
        Fixture.CreateDbContext(new AuditableInterceptor(Fixture.DateTimeProvider, Fixture.UserProvider));

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task SavingChanges_WithNewAuditableEntity_ShouldSetDateCreatedAndDateModified(bool useAsync)
    {
        // Arrange
        var entity = new AuditableEntity { Name = "Test" };
        Db.AuditableEntities.Add(entity);

        // Act
        if (useAsync) await Db.SaveChangesAsync();
        else Db.SaveChanges();

        // Assert
        entity.DateCreatedUtc.Should().Be(Fixture.DateTimeProvider.UtcNow);
        entity.DateModifiedUtc.Should().Be(Fixture.DateTimeProvider.UtcNow);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task SavingChanges_WithModifiedAuditableEntity_ShouldUpdateDateModified(bool useAsync)
    {
        // Arrange
        var createdAt = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        Fixture.DateTimeProvider.UtcNow = createdAt;

        var entity = new AuditableEntity { Name = "Test" };
        Db.AuditableEntities.Add(entity);
        if (useAsync) await Db.SaveChangesAsync();
        else Db.SaveChanges();

        var modifiedAt = new DateTime(2025, 1, 2, 0, 0, 0, DateTimeKind.Utc);
        Fixture.DateTimeProvider.UtcNow = modifiedAt;

        // Act
        entity.Name = "Updated";
        if (useAsync) await Db.SaveChangesAsync();
        else Db.SaveChanges();

        // Assert
        entity.DateCreatedUtc.Should().Be(createdAt);
        entity.DateModifiedUtc.Should().Be(modifiedAt);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task SavingChanges_WithNewAuditableWithUserEntity_ShouldSetCreatedByAndModifiedBy(bool useAsync)
    {
        // Arrange
        var userId = Guid.NewGuid();
        Fixture.UserProvider.UserId = userId;

        var entity = new AuditableWithUserEntity { Name = "Test" };
        Db.AuditableWithUserEntities.Add(entity);

        // Act
        if (useAsync) await Db.SaveChangesAsync();
        else Db.SaveChanges();

        // Assert
        entity.CreatedBy.Should().Be(userId);
        entity.ModifiedBy.Should().Be(userId);
        entity.DateCreatedUtc.Should().Be(Fixture.DateTimeProvider.UtcNow);
        entity.DateModifiedUtc.Should().Be(Fixture.DateTimeProvider.UtcNow);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task SavingChanges_WithModifiedAuditableWithUserEntity_ShouldUpdateModifiedBy(bool useAsync)
    {
        // Arrange
        var creatorId = Guid.NewGuid();
        Fixture.UserProvider.UserId = creatorId;

        var entity = new AuditableWithUserEntity { Name = "Test" };
        Db.AuditableWithUserEntities.Add(entity);
        if (useAsync) await Db.SaveChangesAsync();
        else Db.SaveChanges();

        var modifierId = Guid.NewGuid();
        Fixture.UserProvider.UserId = modifierId;

        // Act
        entity.Name = "Updated";
        if (useAsync) await Db.SaveChangesAsync();
        else Db.SaveChanges();

        // Assert
        entity.CreatedBy.Should().Be(creatorId);
        entity.ModifiedBy.Should().Be(modifierId);
    }
}
