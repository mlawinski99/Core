using Core.DataAccessTypes;
using Core.IntegrationTests.Shared;
using Core.IntegrationTests.Shared.Fixtures;
using Core.IntegrationTests.Shared.Infrastructure;
using Core.IntegrationTests.Shared.Infrastructure.TestEntities;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Core.InfrastructureTests.DataAccessTypes;

[Collection("DataAccessTypesTest")]
public class VersionableInterceptorTests : IntegrationTestBase<IntegrationTestFixture>
{
    public VersionableInterceptorTests(IntegrationTestFixture fixture) : base(fixture)
    {
    }

    protected override TestDbContext CreateDbContext() =>
        Fixture.CreateDbContext(new VersionableInterceptor());

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
