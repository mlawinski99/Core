using Core.IntegrationTests.Shared;
using Core.IntegrationTests.Shared.Fixtures;
using Core.IntegrationTests.Shared.Infrastructure;
using Core.IntegrationTests.Shared.Infrastructure.TestEntities;
using Core.DataAccessTypes;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Core.InfrastructureTests.DataAccessTypes;

[Collection("DataAccessTypesTest")]
public class EncryptableInterceptorTests : IntegrationTestBase<IntegrationTestFixture>
{
    private readonly TestDbContext _dbWithoutInterceptor;

    public EncryptableInterceptorTests(IntegrationTestFixture fixture) : base(fixture)
    {
        _dbWithoutInterceptor = fixture.CreateDbContext();
    }

    protected override TestDbContext CreateDbContext() =>
        Fixture.CreateDbContext(new EncryptableInterceptor(new TestEncryptor()));

    public override void Dispose()
    {
        _dbWithoutInterceptor.Dispose();
        base.Dispose();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task SavingChanges_WithEncryptableProperty_ShouldEncryptValue(bool useAsync)
    {
        // Arrange
        var entity = new EncryptableEntity
        {
            Name = "Test",
            Secret = "secret"
        };

        // Act
        Db.EncryptableEntities.Add(entity);
        if (useAsync) await Db.SaveChangesAsync();
        else Db.SaveChanges();

        // Assert
        var rawEntity = await _dbWithoutInterceptor.EncryptableEntities
            .AsNoTracking()
            .FirstAsync(e => e.Id == entity.Id);

        rawEntity.Secret.Should().Be("encryptedsecret");
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Loading_WithEncryptableProperty_ShouldDecryptValue(bool useAsync)
    {
        // Arrange
        var entity = new EncryptableEntity
        {
            Name = "Test",
            Secret = "secret"
        };
        Db.EncryptableEntities.Add(entity);
        if (useAsync) await Db.SaveChangesAsync();
        else Db.SaveChanges();

        // Act
        var loadedEntity = await Db.EncryptableEntities
            .AsNoTracking()
            .FirstAsync(e => e.Id == entity.Id);

        // Assert
        loadedEntity.Secret.Should().Be("secret");
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task SavingChanges_WithNonEncryptableProperty_ShouldNotEncrypt(bool useAsync)
    {
        // Arrange
        var entity = new EncryptableEntity
        {
            Name = "name",
            Secret = "secret"
        };

        // Act
        Db.EncryptableEntities.Add(entity);
        if (useAsync) await Db.SaveChangesAsync();
        else Db.SaveChanges();

        // Assert
        var rawEntity = await _dbWithoutInterceptor.EncryptableEntities
            .AsNoTracking()
            .FirstAsync(e => e.Id == entity.Id);

        rawEntity.Name.Should().Be("name");
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task SavingChanges_WithEmptyEncryptableProperty_ShouldNotEncrypt(bool useAsync)
    {
        // Arrange
        var entity = new EncryptableEntity
        {
            Name = "Test",
            Secret = ""
        };

        // Act
        Db.EncryptableEntities.Add(entity);
        if (useAsync) await Db.SaveChangesAsync();
        else Db.SaveChanges();

        // Assert
        var rawEntity = await _dbWithoutInterceptor.EncryptableEntities
            .AsNoTracking()
            .FirstAsync(e => e.Id == entity.Id);

        rawEntity.Secret.Should().Be("");
    }
}
