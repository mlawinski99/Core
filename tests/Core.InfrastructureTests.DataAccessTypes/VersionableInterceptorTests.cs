using Core.DataAccessTypes;
using Core.IntegrationTests.Shared;
using Core.IntegrationTests.Shared.Fixtures;
using Core.IntegrationTests.Shared.Infrastructure;
using Core.IntegrationTests.Shared.Infrastructure.TestEntities;
using Core.Tests.Shared;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Core.InfrastructureTests.DataAccessTypes;

[Collection("DataAccessTypesTest")]
public class VersionableInterceptorTests(PostgresFixture postgresFixture) : IntegrationTestBase(postgresFixture)
{
    protected override TestDbContext CreateDbContext() =>
        PostgresFixture.CreateDbContext(new VersionableInterceptor(ExpectedVersionProvider, new TestLogger<VersionableInterceptor>()));

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task SavingChanges_WithModifiedVersionableEntity_ShouldCreateNewVersion(bool useAsync)
    {
        // Arrange
        var entity = new VersionableEntity { Name = "Original", VersionId = 1 };
        Db.VersionableEntities.Add(entity);
        if (useAsync) await Db.SaveChangesAsync();
        else Db.SaveChanges();
        var originalId = entity.Id;

        Db.Entry(entity).State = EntityState.Detached;
        var loadedEntity = await Db.VersionableEntities.FirstAsync(e => e.Id == originalId);

        // Act
        loadedEntity.Name = "Updated";
        if (useAsync) await Db.SaveChangesAsync();
        else Db.SaveChanges();

        // Assert
        var allVersions = await Db.VersionableEntities
            .Where(e => e.Id == originalId || e.VersionGroupId == originalId)
            .OrderBy(e => e.VersionId)
            .ToListAsync();

        allVersions.Should().HaveCount(2);
        allVersions[0].VersionId.Should().Be(1);
        allVersions[0].Name.Should().Be("Original");
        allVersions[1].VersionId.Should().Be(2);
        allVersions[1].Name.Should().Be("Updated");
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task SavingChanges_WithModifiedVersionableEntity_ShouldSetVersionGroupId(bool useAsync)
    {
        // Arrange
        var entity = new VersionableEntity { Name = "Original", VersionId = 1 };
        Db.VersionableEntities.Add(entity);
        if (useAsync) await Db.SaveChangesAsync();
        else Db.SaveChanges();
        var originalId = entity.Id;

        Db.Entry(entity).State = EntityState.Detached;
        var loadedEntity = await Db.VersionableEntities.FirstAsync(e => e.Id == originalId);

        // Act
        loadedEntity.Name = "Updated";
        if (useAsync) await Db.SaveChangesAsync();
        else Db.SaveChanges();

        // Assert
        var allVersions = await Db.VersionableEntities
            .Where(e => e.Id == originalId || e.VersionGroupId == originalId)
            .OrderBy(e => e.VersionId)
            .ToListAsync();

        allVersions[1].VersionGroupId.Should().Be(originalId);
    }

    [Fact]
    public async Task SavingChanges_WithConcurrentModifications_ShouldNotDuplicateVersion()
    {
        // Arrange
        var entity = new VersionableEntity { Name = "Original", VersionId = 1 };
        Db.VersionableEntities.Add(entity);
        await Db.SaveChangesAsync();
        var originalId = entity.Id;

        await using var firstDb = PostgresFixture.CreateDbContext(new VersionableInterceptor(ExpectedVersionProvider, new TestLogger<VersionableInterceptor>()));
        await using var secondDb = PostgresFixture.CreateDbContext(new VersionableInterceptor(ExpectedVersionProvider, new TestLogger<VersionableInterceptor>()));

        var firstEntity = await firstDb.VersionableEntities.FirstAsync(e => e.Id == originalId);
        var secondEntity = await secondDb.VersionableEntities.FirstAsync(e => e.Id == originalId);

        // Act
        firstEntity.Name = "First";
        await firstDb.SaveChangesAsync();

        secondEntity.Name = "Second";
        var staleSave = async () => await secondDb.SaveChangesAsync();

        // Assert
        await staleSave.Should().ThrowAsync<DbUpdateConcurrencyException>();

        var allVersions = await Db.VersionableEntities
            .AsNoTracking()
            .Where(e => e.Id == originalId || e.VersionGroupId == originalId)
            .OrderBy(e => e.VersionId)
            .ToListAsync();

        allVersions.Should().HaveCount(2);
        allVersions.Count(e => e.VersionId == 1).Should().Be(1);
        allVersions.Count(e => e.VersionId == 2).Should().Be(1);
    }

    [Fact]
    public async Task SavingChanges_WithStaleExpectedVersion_ShouldThrowAndNotVersion()
    {
        // Arrange
        var entity = new VersionableEntity { Name = "Original", VersionId = 1 };
        Db.VersionableEntities.Add(entity);
        await Db.SaveChangesAsync();
        var originalId = entity.Id;

        entity.Name = "Updated";
        await Db.SaveChangesAsync();

        // Act
        ExpectedVersionProvider.ExpectedVersion = 1;
        entity.Name = "Stale";
        var staleSave = async () => await Db.SaveChangesAsync();

        // Assert
        await staleSave.Should().ThrowAsync<DbUpdateConcurrencyException>();

        Db.ChangeTracker.Clear();
        var allVersions = await Db.VersionableEntities
            .AsNoTracking()
            .Where(e => e.Id == originalId || e.VersionGroupId == originalId)
            .ToListAsync();

        allVersions.Should().HaveCount(2);
        allVersions.Single(e => e.Id == originalId).Name.Should().Be("Updated");
    }

    [Fact]
    public async Task SavingChanges_WithCurrentExpectedVersion_ShouldSave()
    {
        // Arrange
        var entity = new VersionableEntity { Name = "Original", VersionId = 1 };
        Db.VersionableEntities.Add(entity);
        await Db.SaveChangesAsync();
        var originalId = entity.Id;

        // Act
        ExpectedVersionProvider.ExpectedVersion = 1;
        entity.Name = "Updated";
        await Db.SaveChangesAsync();

        // Assert
        Db.ChangeTracker.Clear();
        var current = await Db.VersionableEntities.AsNoTracking().FirstAsync(e => e.Id == originalId);

        current.Name.Should().Be("Updated");
        current.VersionId.Should().Be(2);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task SavingChanges_WithNewVersionableEntity_ShouldNotCreateExtraVersion(bool useAsync)
    {
        // Arrange Act
        var entity = new VersionableEntity { Name = "New", VersionId = 1 };
        Db.VersionableEntities.Add(entity);
        if (useAsync) await Db.SaveChangesAsync();
        else Db.SaveChanges();

        // Assert
        var count = await Db.VersionableEntities.CountAsync(e => e.Id == entity.Id);
        count.Should().Be(1);
    }
}
