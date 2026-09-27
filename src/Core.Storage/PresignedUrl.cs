namespace Core.Storage;

public record PresignedUrl(Uri Url, DateTime ExpiresAt);