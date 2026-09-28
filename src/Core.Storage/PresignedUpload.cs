namespace Core.Storage;

// file last field in dict
public record PresignedUpload(Uri Url, IReadOnlyDictionary<string, string> Fields, DateTime ExpiresAt);
