using Amazon.S3;
using Amazon.S3.Model;
using Core.DataAccessTypes;
using Core.IntegrationTests.Shared;
using Core.IntegrationTests.Shared.Fixtures;
using Core.IntegrationTests.Shared.Infrastructure;
using Core.ResultPattern;
using Core.Storage;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Xunit;

namespace Core.InfrastructureTests.Storage;

[Collection("Storage")]
public class FileServiceTests(PostgresFixture postgresFixture, GarageFixture garageFixture)
    : IntegrationTestBase(postgresFixture)
{
    private readonly Guid _userId = Guid.NewGuid();
    private readonly HttpClient _httpClient = new();

    private IAmazonS3 _s3Client = null!;
    private S3StorageService _storageService = null!;
    private FileService _fileService = null!;

    protected override TestDbContext CreateDbContext() =>
        PostgresFixture.CreateDbContext(
            new AuditableInterceptor(DateTimeProvider, UserProvider),
            new SoftDeletableInterceptor(DateTimeProvider));

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();

        UserProvider.UserId = _userId;

        var options = garageFixture.CreateS3Options();
        var fileOptions = Options.Create(new StoredFileOptions
        {
            MaxFileSizeInMb = 1,
            AllowedContentTypes = ["application/pdf", "text/plain"]
        });
        _s3Client = StorageDependencyInstaller.CreateClient(options);
        // TestDateTimeProvider cant be used here - real time check
        _storageService = new S3StorageService(_s3Client, new Core.DateTimeProvider.DateTimeProvider(), Options.Create(options), fileOptions);
        _fileService = new FileService(Db, _storageService, fileOptions);

        await Db.StoredFiles.IgnoreQueryFilters().ExecuteDeleteAsync();
    }

    public override async Task DisposeAsync()
    {
        if (Db is not null)
            await Db.StoredFiles.IgnoreQueryFilters().ExecuteDeleteAsync();

        _s3Client.Dispose();
        _httpClient.Dispose();

        await base.DisposeAsync();
    }

    [Fact]
    public async Task CreateUpload_ShouldAddPendingFileWithKeyIndependentOfId()
    {
        // Act
        var upload = _fileService.CreateUpload("report.pdf", "application/pdf").Data!;
        await Db.SaveChangesAsync();

        // Assert
        var file = await Db.StoredFiles.AsNoTracking().FirstAsync(f => f.Id == upload.FileId);
        file.Key.Should().NotBe(upload.FileId.ToString());
        Guid.TryParse(file.Key, out _).Should().BeTrue();
        file.FileName.Should().Be("report.pdf");
        file.ContentType.Should().Be("application/pdf");
        file.Status.Should().Be(StoredFileStatus.Pending);
        file.SizeInBytes.Should().BeNull();
        file.CreatedBy.Should().Be(_userId);
    }

    [Fact]
    public async Task CreateUpload_WithContentTypeNotAllowed_ShouldReturnBadRequestWithoutAddingFile()
    {
        // Act
        var result = _fileService.CreateUpload("page.html", "text/html");
        await Db.SaveChangesAsync();

        // Assert
        result.Code.Should().Be(ResultCode.BadRequest);
        var fileCount = await Db.StoredFiles.CountAsync();
        fileCount.Should().Be(0);
    }

    [Fact]
    public void CreateUpload_WithAllowedContentTypeInDifferentCase_ShouldSucceed()
    {
        // Act
        var result = _fileService.CreateUpload("report.pdf", "Application/PDF");

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task Confirm_WithUploadedObject_ShouldMarkFileUploadedWithActualSize()
    {
        // Arrange
        var upload = _fileService.CreateUpload("notes.txt", "text/plain").Data!;
        await Db.SaveChangesAsync();
        var form = new MultipartFormDataContent();
        foreach (var (name, value) in upload.Upload.Fields)
            form.Add(new StringContent(value), name);
        form.Add(new ByteArrayContent("uploaded"u8.ToArray()), "file", "notes.txt");
        await _httpClient.PostAsync(upload.Upload.Url, form);

        // Act
        var result = await _fileService.Confirm(upload.FileId, _userId);
        await Db.SaveChangesAsync();

        // Assert
        result.IsSuccess.Should().BeTrue();
        var file = await Db.StoredFiles.AsNoTracking().FirstAsync(f => f.Id == upload.FileId);
        file.Status.Should().Be(StoredFileStatus.Uploaded);
        file.SizeInBytes.Should().Be(8);
        file.ContentType.Should().Be("text/plain");
    }

    [Fact]
    public async Task Confirm_WithoutUploadedObject_ShouldReturnUnprocessableEntityAndStayPending()
    {
        // Arrange
        var upload = _fileService.CreateUpload("notes.txt", "text/plain").Data!;
        await Db.SaveChangesAsync();

        // Act
        var result = await _fileService.Confirm(upload.FileId, _userId);
        await Db.SaveChangesAsync();

        // Assert
        result.Code.Should().Be(ResultCode.UnprocessableEntity);
        var file = await Db.StoredFiles.AsNoTracking().FirstAsync(f => f.Id == upload.FileId);
        file.Status.Should().Be(StoredFileStatus.Pending);
    }

    [Fact]
    public async Task Confirm_WithAlreadyUploadedFile_ShouldReturnSuccess()
    {
        // Arrange
        var upload = _fileService.CreateUpload("notes.txt", "text/plain").Data!;
        await Db.SaveChangesAsync();
        var key = await Db.StoredFiles.Where(f => f.Id == upload.FileId).Select(f => f.Key).FirstAsync();
        await _s3Client.PutObjectAsync(new PutObjectRequest
        {
            BucketName = GarageFixture.Bucket,
            Key = key,
            ContentBody = "uploaded",
            ContentType = "text/plain"
        });
        await _fileService.Confirm(upload.FileId, _userId);
        await Db.SaveChangesAsync();

        // Act
        var result = await _fileService.Confirm(upload.FileId, _userId);

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task Confirm_WithFileOfAnotherUser_ShouldReturnNotFound()
    {
        // Arrange
        var upload = _fileService.CreateUpload("notes.txt", "text/plain").Data!;
        await Db.SaveChangesAsync();
        var key = await Db.StoredFiles.Where(f => f.Id == upload.FileId).Select(f => f.Key).FirstAsync();
        await _s3Client.PutObjectAsync(new PutObjectRequest
        {
            BucketName = GarageFixture.Bucket,
            Key = key,
            ContentBody = "uploaded",
            ContentType = "text/plain"
        });

        // Act
        var result = await _fileService.Confirm(upload.FileId, Guid.NewGuid());

        // Assert
        result.Code.Should().Be(ResultCode.NotFound);
    }

    [Fact]
    public async Task Confirm_WithUnknownFile_ShouldReturnNotFound()
    {
        // Act
        var result = await _fileService.Confirm(Guid.NewGuid(), _userId);

        // Assert
        result.Code.Should().Be(ResultCode.NotFound);
    }

    [Fact]
    public async Task GetDownloadUrl_WithUploadedFile_ShouldServeItsContent()
    {
        // Arrange
        var upload = _fileService.CreateUpload("notes.txt", "text/plain").Data!;
        await Db.SaveChangesAsync();
        var key = await Db.StoredFiles.Where(f => f.Id == upload.FileId).Select(f => f.Key).FirstAsync();
        await _s3Client.PutObjectAsync(new PutObjectRequest
        {
            BucketName = GarageFixture.Bucket,
            Key = key,
            ContentBody = "uploaded",
            ContentType = "text/plain"
        });
        await _fileService.Confirm(upload.FileId, _userId);
        await Db.SaveChangesAsync();

        // Act
        var result = await _fileService.GetDownloadUrl(upload.FileId);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var content = await _httpClient.GetStringAsync(result.Data!.Url);
        content.Should().Be("uploaded");
    }

    [Fact]
    public async Task GetDownloadUrl_WithPendingFile_ShouldReturnNotFound()
    {
        // Arrange
        var upload = _fileService.CreateUpload("notes.txt", "text/plain").Data!;
        await Db.SaveChangesAsync();

        // Act
        var result = await _fileService.GetDownloadUrl(upload.FileId);

        // Assert
        result.Code.Should().Be(ResultCode.NotFound);
    }

    [Fact]
    public async Task Delete_WithUploadedFile_ShouldSoftDeleteRowAndKeepObject()
    {
        // Arrange
        var upload = _fileService.CreateUpload("notes.txt", "text/plain").Data!;
        await Db.SaveChangesAsync();
        var key = await Db.StoredFiles.Where(f => f.Id == upload.FileId).Select(f => f.Key).FirstAsync();
        await _s3Client.PutObjectAsync(new PutObjectRequest
        {
            BucketName = GarageFixture.Bucket,
            Key = key,
            ContentBody = "uploaded",
            ContentType = "text/plain"
        });

        // Act
        var result = await _fileService.Delete(upload.FileId);
        await Db.SaveChangesAsync();

        // Assert
        result.IsSuccess.Should().BeTrue();
        var file = await Db.StoredFiles.IgnoreQueryFilters().AsNoTracking().FirstAsync(f => f.Id == upload.FileId);
        file.IsDeleted.Should().BeTrue();
        var exists = await _storageService.GetMetadata(key) is not null;
        exists.Should().BeTrue();
    }

    [Fact]
    public async Task Delete_WithUnknownFile_ShouldReturnNotFound()
    {
        // Act
        var result = await _fileService.Delete(Guid.NewGuid());

        // Assert
        result.Code.Should().Be(ResultCode.NotFound);
    }
}
