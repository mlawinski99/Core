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
public class SoftDeletableInterceptorTests : IntegrationTestBase<IntegrationTestFixture>
{
    public SoftDeletableInterceptorTests(IntegrationTestFixture fixture) : base(fixture)
    {
    }

    protected override TestDbContext CreateDbContext() =>
        Fixture.CreateDbContext(new SoftDeletableInterceptor(Fixture.DateTimeProvider));

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task SavingChanges_WithDeletedSoftDeletableEntity_ShouldSetIsDeletedAndDateDeleted(bool useAsync)
    {
        // Arrange
        var entity = new SoftDeletableEntity { Name = "Test" };
        Db.SoftDeletableEntities.Add(entity);
        if (useAsync) await Db.SaveChangesAsync();
        else Db.SaveChanges();

        var deleteTime = new DateTime(2025, 1, 15, 12, 0, 0, DateTimeKind.Utc);
        Fixture.DateTimeProvider.UtcNow = deleteTime;

        // Act
        Db.SoftDeletableEntities.Remove(entity);
        if (useAsync) await Db.SaveChangesAsync();
        else Db.SaveChanges();

        // Assert
        var deletedEntity = await Db.SoftDeletableEntities
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(e => e.Id == entity.Id);

        deletedEntity.Should().NotBeNull();
        deletedEntity.IsDeleted.Should().BeTrue();
        deletedEntity.DateDeletedUtc.Should().Be(deleteTime);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task SavingChanges_WithDeletedSoftDeletableEntity_ShouldNotHardDelete(bool useAsync)
    {
        // Arrange
        var entity = new SoftDeletableEntity { Name = "Test" };
        Db.SoftDeletableEntities.Add(entity);
        if (useAsync) await Db.SaveChangesAsync();
        else Db.SaveChanges();
        var entityId = entity.Id;

        // Act
        Db.SoftDeletableEntities.Remove(entity);
        if (useAsync) await Db.SaveChangesAsync();
        else Db.SaveChanges();

        // Assert
        var countWithoutFilter = await Db.SoftDeletableEntities
            .IgnoreQueryFilters()
            .CountAsync(e => e.Id == entityId);

        countWithoutFilter.Should().Be(1);

        var countWithFilter = await Db.SoftDeletableEntities
            .CountAsync(e => e.Id == entityId);

        countWithFilter.Should().Be(0);
    }
}
