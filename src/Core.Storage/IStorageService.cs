namespace Core.Storage;

internal interface IStorageService
{
    Task<StoredObjectMetadata?> GetMetadata(string key, CancellationToken cancellationToken = default);

    Task Delete(IReadOnlyCollection<string> keys, CancellationToken cancellationToken = default);

    PresignedUpload CreatePresignedUpload(string key, string contentType, TimeSpan? expiration = null);

    PresignedUrl GetDownloadUrl(string key, TimeSpan? expiration = null);
}