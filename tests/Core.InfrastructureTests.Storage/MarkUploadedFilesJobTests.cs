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
public class MarkUploadedFilesJobTests(PostgresFixture postgresFixture, GarageFixture garageFixture)
    : IntegrationTestBase(postgresFixture)
{
    private readonly StoredFileOptions _options = new() { PendingExpiration = TimeSpan.FromHours(24), BatchSize = 2 };

    private IAmazonS3 _s3Client = null!;
    private S3StorageService _storageService = null!;
    private MarkUploadedFilesJob<TestDbContext> _job = null!;

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();

        var options = garageFixture.CreateS3Options();
        _s3Client = StorageDependencyInstaller.CreateClient(options);
        _storageService = new S3StorageService(_s3Client, DateTimeProvider, Options.Create(options), Options.Create(_options));
        _job = new MarkUploadedFilesJob<TestDbContext>(
            Db,
            new FileService(Db, _storageService, DateTimeProvider, UserProvider, Options.Create(_options)),
            new TestLogger<MarkUploadedFilesJob<TestDbContext>>(),
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
    public async Task Run_WithUploadedPendingFile_ShouldMarkItUploaded()
    {
        // Arrange
        var id = Guid.NewGuid();
        Db.StoredFiles.Add(new StoredFile
        {
            Id = id,
            Key = id.ToString(),
            FileName = "notes.txt",
            ContentType = "text/plain",
            Status = StoredFileStatus.Pending,
            DateCreatedUtc = DateTimeProvider.UtcNow
        });
        await Db.SaveChangesAsync();
        await _s3Client.PutObjectAsync(new PutObjectRequest
        {
            BucketName = GarageFixture.Bucket,
            Key = id.ToString(),
            ContentBody = "uploaded",
            ContentType = "text/plain"
        });

        // Act
        await _job.Run(CancellationToken.None);

        // Assert
        var file = await Db.StoredFiles.AsNoTracking().FirstAsync(f => f.Id == id);
        file.Status.Should().Be(StoredFileStatus.Uploaded);
        file.SizeInBytes.Should().Be(8);
    }

    [Fact]
    public async Task Run_WithPendingFileNotYetUploaded_ShouldKeepItPending()
    {
        // Arrange
        var id = Guid.NewGuid();
        Db.StoredFiles.Add(new StoredFile
        {
            Id = id,
            Key = id.ToString(),
            FileName = "notes.txt",
            ContentType = "text/plain",
            Status = StoredFileStatus.Pending,
            DateCreatedUtc = DateTimeProvider.UtcNow
        });
        await Db.SaveChangesAsync();

        // Act
        await _job.Run(CancellationToken.None);

        // Assert
        var file = await Db.StoredFiles.AsNoTracking().FirstAsync(f => f.Id == id);
        file.Status.Should().Be(StoredFileStatus.Pending);
    }

    [Fact]
    public async Task Run_WithPendingFileOlderThanExpiration_ShouldNotEdit() // will be cleaned by other job
    {
        // Arrange
        var id = Guid.NewGuid();
        Db.StoredFiles.Add(new StoredFile
        {
            Id = id,
            Key = id.ToString(),
            FileName = "notes.txt",
            ContentType = "text/plain",
            Status = StoredFileStatus.Pending,
            DateCreatedUtc = DateTimeProvider.UtcNow.AddHours(-25)
        });
        await Db.SaveChangesAsync();
        await _s3Client.PutObjectAsync(new PutObjectRequest
        {
            BucketName = GarageFixture.Bucket,
            Key = id.ToString(),
            ContentBody = "uploaded",
            ContentType = "text/plain"
        });

        // Act
        await _job.Run(CancellationToken.None);

        // Assert
        var file = await Db.StoredFiles.AsNoTracking().FirstAsync(f => f.Id == id);
        file.Status.Should().Be(StoredFileStatus.Pending);
    }

    [Fact]
    public async Task Run_WithMoreFilesThanBatchSizeAndSomeNotUploaded_ShouldMarkEveryUploadedFile()
    {
        // Arrange
        var ids = Enumerable.Range(0, 5).Select(_ => Guid.NewGuid()).ToList();
        Db.StoredFiles.AddRange(ids.Select(id => new StoredFile
        {
            Id = id,
            Key = id.ToString(),
            FileName = "notes.txt",
            ContentType = "text/plain",
            Status = StoredFileStatus.Pending,
            DateCreatedUtc = DateTimeProvider.UtcNow
        }));
        await Db.SaveChangesAsync();
        var uploadedIds = ids.Where((_, index) => index % 2 == 1).ToList();
        foreach (var id in uploadedIds)
        {
            await _s3Client.PutObjectAsync(new PutObjectRequest
            {
                BucketName = GarageFixture.Bucket,
                Key = id.ToString(),
                ContentBody = "uploaded",
                ContentType = "text/plain"
            });
        }

        // Act
        await _job.Run(CancellationToken.None);

        // Assert
        var markedIds = await Db.StoredFiles
            .Where(f => f.Status == StoredFileStatus.Uploaded)
            .Select(f => f.Id)
            .ToListAsync();
        markedIds.Should().BeEquivalentTo(uploadedIds);
    }

    [Fact]
    public async Task Run_WithFileThatFailsToMark_ShouldMarkOtherFiles()
    {
        // Arrange
        var failingId = Guid.NewGuid();
        var uploadedId = Guid.NewGuid();
        Db.StoredFiles.AddRange(
            new StoredFile
            {
                Id = failingId,
                Key = "", // rejected by the S3 client, so marking throws
                FileName = "notes.txt",
                ContentType = "text/plain",
                Status = StoredFileStatus.Pending,
                DateCreatedUtc = DateTimeProvider.UtcNow.AddMinutes(-1)
            },
            new StoredFile
            {
                Id = uploadedId,
                Key = uploadedId.ToString(),
                FileName = "notes.txt",
                ContentType = "text/plain",
                Status = StoredFileStatus.Pending,
                DateCreatedUtc = DateTimeProvider.UtcNow
            });
        await Db.SaveChangesAsync();
        await _s3Client.PutObjectAsync(new PutObjectRequest
        {
            BucketName = GarageFixture.Bucket,
            Key = uploadedId.ToString(),
            ContentBody = "uploaded",
            ContentType = "text/plain"
        });

        // Act
        await _job.Run(CancellationToken.None);

        // Assert
        var statuses = await Db.StoredFiles.AsNoTracking().ToDictionaryAsync(f => f.Id, f => f.Status);
        statuses[failingId].Should().Be(StoredFileStatus.Pending);
        statuses[uploadedId].Should().Be(StoredFileStatus.Uploaded);
    }
}
