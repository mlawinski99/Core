using Amazon.S3;
using Amazon.S3.Model;
using Core.IntegrationTests.Shared;
using Core.IntegrationTests.Shared.Fixtures;
using Core.IntegrationTests.Shared.Infrastructure;
using Core.Storage;
using Core.Tests.Shared;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Xunit;

namespace Core.InfrastructureTests.Storage;

[Collection("Storage")]
public class CleanupStoredFilesJobTests(PostgresFixture postgresFixture, GarageFixture garageFixture)
    : IntegrationTestBase(postgresFixture)
{
    private readonly StoredFileOptions _options = new() { PendingExpiration = TimeSpan.FromHours(24), BatchSize = 2 };

    private IAmazonS3 _s3Client = null!;
    private S3StorageService _storageService = null!;
    private CleanupStoredFilesJob<TestDbContext> _job = null!;

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();

        var options = garageFixture.CreateS3Options();
        _s3Client = StorageDependencyInstaller.CreateClient(options);
        _storageService = new S3StorageService(_s3Client, DateTimeProvider, Options.Create(options), Options.Create(_options));
        _job = new CleanupStoredFilesJob<TestDbContext>(
            Db,
            _storageService,
            new TestLogger<CleanupStoredFilesJob<TestDbContext>>(),
            DateTimeProvider,
            Options.Create(_options));

        await Db.StoredFiles.IgnoreQueryFilters().ExecuteDeleteAsync();
    }

    public override async Task DisposeAsync()
    {
        if (Db is not null)
            await Db.StoredFiles.IgnoreQueryFilters().ExecuteDeleteAsync();

        _s3Client.Dispose();

        await base.DisposeAsync();
    }

    [Fact]
    public async Task Run_WithDeletedFile_ShouldDeleteObjectAndRow()
    {
        // Arrange
        var id = Guid.NewGuid();
        Db.StoredFiles.Add(new StoredFile
        {
            Id = id,
            Key = id.ToString(),
            FileName = "deleted.txt",
            ContentType = "text/plain",
            Status = StoredFileStatus.Uploaded,
            DateCreatedUtc = DateTimeProvider.UtcNow,
            IsDeleted = true
        });
        await Db.SaveChangesAsync();
        await _s3Client.PutObjectAsync(new PutObjectRequest
        {
            BucketName = GarageFixture.Bucket,
            Key = id.ToString(),
            ContentBody = "stored",
            ContentType = "text/plain"
        });

        // Act
        await _job.Run(CancellationToken.None);

        // Assert
        var rowExists = await Db.StoredFiles.IgnoreQueryFilters().AnyAsync(f => f.Id == id);
        rowExists.Should().BeFalse();
        var objectExists = await _storageService.GetMetadata(id.ToString()) is not null;
        objectExists.Should().BeFalse();
    }

    [Fact]
    public async Task Run_WithPendingFileOlderThanExpirationAndCleanupDelay_ShouldDeleteObjectAndRow()
    {
        // Arrange
        var id = Guid.NewGuid();
        Db.StoredFiles.Add(new StoredFile
        {
            Id = id,
            Key = id.ToString(),
            FileName = "abandoned.txt",
            ContentType = "text/plain",
            Status = StoredFileStatus.Pending,
            DateCreatedUtc = DateTimeProvider.UtcNow.AddHours(-26)
        });
        await Db.SaveChangesAsync();
        await _s3Client.PutObjectAsync(new PutObjectRequest
        {
            BucketName = GarageFixture.Bucket,
            Key = id.ToString(),
            ContentBody = "stored",
            ContentType = "text/plain"
        });

        // Act
        await _job.Run(CancellationToken.None);

        // Assert
        var rowExists = await Db.StoredFiles.IgnoreQueryFilters().AnyAsync(f => f.Id == id);
        rowExists.Should().BeFalse();
        var objectExists = await _storageService.GetMetadata(id.ToString()) is not null;
        objectExists.Should().BeFalse();
    }

    [Fact]
    public async Task Run_WithExpiredPendingFileInsideCleanupDelay_ShouldKeepIt()
    {
        // Arrange
        var id = Guid.NewGuid();
        Db.StoredFiles.Add(new StoredFile
        {
            Id = id,
            Key = id.ToString(),
            FileName = "expired.txt",
            ContentType = "text/plain",
            Status = StoredFileStatus.Pending,
            DateCreatedUtc = DateTimeProvider.UtcNow.AddHours(-24).AddMinutes(-30)
        });
        await Db.SaveChangesAsync();

        // Act
        await _job.Run(CancellationToken.None);

        // Assert
        var rowExists = await Db.StoredFiles.AnyAsync(f => f.Id == id);
        rowExists.Should().BeTrue();
    }

    [Fact]
    public async Task Run_WithPendingFileInsideExpiration_ShouldKeepIt()
    {
        // Arrange
        var id = Guid.NewGuid();
        Db.StoredFiles.Add(new StoredFile
        {
            Id = id,
            Key = id.ToString(),
            FileName = "in-progress.txt",
            ContentType = "text/plain",
            Status = StoredFileStatus.Pending,
            DateCreatedUtc = DateTimeProvider.UtcNow.AddHours(-23)
        });
        await Db.SaveChangesAsync();
        await _s3Client.PutObjectAsync(new PutObjectRequest
        {
            BucketName = GarageFixture.Bucket,
            Key = id.ToString(),
            ContentBody = "stored",
            ContentType = "text/plain"
        });

        // Act
        await _job.Run(CancellationToken.None);

        // Assert
        var rowExists = await Db.StoredFiles.AnyAsync(f => f.Id == id);
        rowExists.Should().BeTrue();

        var objectExists = await _storageService.GetMetadata(id.ToString()) is not null;
        objectExists.Should().BeTrue();
    }

    [Fact]
    public async Task Run_WithUploadedFile_ShouldKeepObjectAndRow()
    {
        // Arrange
        var id = Guid.NewGuid();
        Db.StoredFiles.Add(new StoredFile
        {
            Id = id,
            Key = id.ToString(),
            FileName = "kept.txt",
            ContentType = "text/plain",
            Status = StoredFileStatus.Uploaded,
            DateCreatedUtc = DateTimeProvider.UtcNow.AddDays(-30)
        });
        await Db.SaveChangesAsync();
        await _s3Client.PutObjectAsync(new PutObjectRequest
        {
            BucketName = GarageFixture.Bucket,
            Key = id.ToString(),
            ContentBody = "stored",
            ContentType = "text/plain"
        });

        // Act
        await _job.Run(CancellationToken.None);

        // Assert
        var rowExists = await Db.StoredFiles.AnyAsync(f => f.Id == id);
        rowExists.Should().BeTrue();
        var objectExists = await _storageService.GetMetadata(id.ToString()) is not null;
        objectExists.Should().BeTrue();
    }

    [Fact]
    public async Task Run_WithMoreDeletedFilesThanBatchSize_ShouldDeleteAllRows()
    {
        // Arrange
        var ids = new[] { Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid() };
        Db.StoredFiles.AddRange(ids.Select(id => new StoredFile
        {
            Id = id,
            Key = id.ToString(),
            FileName = "deleted.txt",
            ContentType = "text/plain",
            Status = StoredFileStatus.Uploaded,
            DateCreatedUtc = DateTimeProvider.UtcNow,
            IsDeleted = true
        }));
        await Db.SaveChangesAsync();

        // Act
        await _job.Run(CancellationToken.None);

        // Assert
        var remaining = await Db.StoredFiles.IgnoreQueryFilters().CountAsync(f => ids.Contains(f.Id));
        remaining.Should().Be(0);
    }
}
