using Amazon.S3;
using Amazon.S3.Model;
using Core.IntegrationTests.Shared.Fixtures;
using Core.IntegrationTests.Shared.Infrastructure;
using Core.Storage;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Core.InfrastructureTests.Storage;

[Collection("Storage")]
public class S3StorageServiceTests : IDisposable
{
    private readonly string _key = $"tests/{Guid.NewGuid()}.txt";
    private readonly S3Options _options;
    private readonly IAmazonS3 _s3Client;
    private readonly HttpClient _httpClient = new();
    private readonly S3StorageService _storageService;

    public S3StorageServiceTests(GarageFixture garageFixture)
    {
        _options = garageFixture.CreateS3Options();

        _s3Client = StorageDependencyInstaller.CreateClient(_options);
        _storageService = new S3StorageService(
            _s3Client,
            new DateTimeProvider.DateTimeProvider(),
            Options.Create(_options),
            Options.Create(new StoredFileOptions { MaxFileSizeInMb = 1 }));
    }

    public void Dispose()
    {
        _s3Client.Dispose();
        _httpClient.Dispose();
    }

    [Fact]
    public async Task GetMetadata_WithUploadedObject_ShouldReturnContentTypeAndSize()
    {
        // Arrange
        await _s3Client.PutObjectAsync(new PutObjectRequest
        {
            BucketName = GarageFixture.Bucket,
            Key = _key,
            ContentBody = "stored",
            ContentType = "text/plain"
        });

        // Act
        var metadata = await _storageService.GetMetadata(_key);

        // Assert
        metadata.Should().NotBeNull();
        metadata.Key.Should().Be(_key);
        metadata.ContentType.Should().Be("text/plain");
        metadata.Size.Should().Be(6);
    }

    [Fact]
    public async Task GetMetadata_WithMissingKey_ShouldReturnNull()
    {
        // Act
        var metadata = await _storageService.GetMetadata(_key);

        // Assert
        metadata.Should().BeNull();
    }

    [Fact]
    public async Task CreatePresignedUpload_WithFileWithinLimit_ShouldAcceptDirectUpload()
    {
        // Arrange
        var upload = _storageService.CreatePresignedUpload(_key, "text/plain");
        var form = new MultipartFormDataContent();
        foreach (var (name, value) in upload.Fields)
            form.Add(new StringContent(value), name);
        form.Add(new ByteArrayContent("direct"u8.ToArray()), "file", "direct.txt");

        // Act
        var response = await _httpClient.PostAsync(upload.Url, form);

        // Assert
        response.IsSuccessStatusCode.Should().BeTrue();
        var metadata = await _storageService.GetMetadata(_key);
        metadata!.ContentType.Should().Be("text/plain");
        metadata.Size.Should().Be(6);
    }

    [Fact]
    public async Task CreatePresignedUpload_WithFileOverLimit_ShouldRejectDirectUpload()
    {
        // Arrange
        var upload = _storageService.CreatePresignedUpload(_key, "application/octet-stream");
        var form = new MultipartFormDataContent();
        foreach (var (name, value) in upload.Fields)
            form.Add(new StringContent(value), name);
        form.Add(new ByteArrayContent(new byte[1024 * 1024 + 1]), "file", "large.bin");

        // Act
        var response = await _httpClient.PostAsync(upload.Url, form);

        // Assert
        response.IsSuccessStatusCode.Should().BeFalse();
        var exists = await _storageService.GetMetadata(_key) is not null;
        exists.Should().BeFalse();
    }

    [Fact]
    public async Task CreatePresignedUpload_WithDifferentContentType_ShouldRejectDirectUpload()
    {
        // Arrange
        var upload = _storageService.CreatePresignedUpload(_key, "image/png");
        var form = new MultipartFormDataContent();
        var fieldsWithoutContentType = upload.Fields.Where(f => f.Key != "Content-Type");
        foreach (var (name, value) in fieldsWithoutContentType)
            form.Add(new StringContent(value), name);
        form.Add(new StringContent("text/html"), "Content-Type");
        form.Add(new ByteArrayContent([.. "direct"u8]), "file", "direct.html");

        // Act
        var response = await _httpClient.PostAsync(upload.Url, form);

        // Assert
        response.IsSuccessStatusCode.Should().BeFalse();
        var exists = await _storageService.GetMetadata(_key) is not null;
        exists.Should().BeFalse();
    }

    [Fact]
    public async Task GetDownloadUrl_WithUploadedObject_ShouldReturnIt()
    {
        // Arrange
        await _s3Client.PutObjectAsync(new PutObjectRequest
        {
            BucketName = GarageFixture.Bucket,
            Key = _key,
            ContentBody = "stored",
            ContentType = "text/plain"
        });
        var downloadUrl = _storageService.GetDownloadUrl(_key);

        // Act
        var content = await _httpClient.GetStringAsync(downloadUrl.Url);

        // Assert
        content.Should().Be("stored");
    }

    [Fact]
    public void GetDownloadUrl_WithoutExpiration_ShouldApplyDefaultExpiration()
    {
        // Arrange
        var dateTimeProvider = new TestDateTimeProvider { UtcNow = new DateTime(2026, 1, 15, 12, 0, 0, DateTimeKind.Utc) };
        var storageService = new S3StorageService(
            _s3Client,
            dateTimeProvider,
            Options.Create(_options),
            Options.Create(new StoredFileOptions()));

        // Act
        var downloadUrl = storageService.GetDownloadUrl(_key);

        // Assert
        downloadUrl.ExpiresAt.Should().Be(dateTimeProvider.UtcNow + _options.DefaultUrlExpiration);
    }
}
