using System.Net;
using Amazon.S3;
using Amazon.S3.Model;
using Core.DateTimeProvider;
using Microsoft.Extensions.Options;

namespace Core.Storage;

internal class S3StorageService(
    IAmazonS3 s3Client,
    IDateTimeProvider dateTimeProvider,
    IOptions<S3Options> options,
    IOptions<StoredFileOptions> fileOptions)
    : IStorageService
{
    private readonly S3Options _options = options.Value;
    private readonly StoredFileOptions _fileOptions = fileOptions.Value;

    public async Task<StoredObjectMetadata?> GetMetadata(string key, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await s3Client.GetObjectMetadataAsync(_options.Bucket, key, cancellationToken);

            return new StoredObjectMetadata(
                key,
                response.Headers.ContentType,
                response.ContentLength,
                response.LastModified.GetValueOrDefault());
        }
        catch (AmazonS3Exception e) when (e.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    // limit 1000; any per-key failure throws DeleteObjectsException
    public async Task Delete(IReadOnlyCollection<string> keys, CancellationToken cancellationToken = default)
    {
        if (keys.Count == 0)
            return;

        await s3Client.DeleteObjectsAsync(new DeleteObjectsRequest
        {
            BucketName = _options.Bucket,
            Objects = [.. keys.Select(key => new KeyVersion { Key = key })],
            Quiet = true
        }, cancellationToken);
    }

    // the signed policy makes S3 reject any other key, content type or a file over the size limit
    public PresignedUpload CreatePresignedUpload(string key, string contentType, TimeSpan? expiration = null)
    {
        var expiresAt = dateTimeProvider.UtcNow + (expiration ?? _options.DefaultUrlExpiration);

        var response = s3Client.CreatePresignedPost(new CreatePresignedPostRequest
        {
            BucketName = _options.Bucket,
            Key = key,
            Expires = expiresAt,
            Fields = new Dictionary<string, string> { ["Content-Type"] = contentType },
            Conditions =
            [
                new ExactMatchCondition("Content-Type", contentType),
                new ContentLengthRangeCondition(1, _fileOptions.MaxFileSizeInBytes)
            ]
        });

        return new PresignedUpload(new Uri(response.Url), response.Fields, expiresAt);
    }

    public PresignedUrl GetDownloadUrl(string key, TimeSpan? expiration = null)
    {
        var expiresAt = dateTimeProvider.UtcNow + (expiration ?? _options.DefaultUrlExpiration);

        var url = s3Client.GetPreSignedURL(new GetPreSignedUrlRequest
        {
            BucketName = _options.Bucket,
            Key = key,
            Verb = HttpVerb.GET,
            Expires = expiresAt,
            Protocol = new Uri(_options.ServiceUrl).Scheme == Uri.UriSchemeHttp ? Protocol.HTTP : Protocol.HTTPS
        });

        return new PresignedUrl(new Uri(url), expiresAt);
    }
}
