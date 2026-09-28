namespace Core.Storage;

internal record StoredObjectMetadata(string Key, string ContentType, long Size, DateTime LastModified);