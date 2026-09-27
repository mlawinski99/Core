namespace Core.Storage;

public class StoredFileOptions
{
    public const string SectionName = "StoredFiles";

    public string MarkUploadedCron { get; init; } = "* * * * *";
    public string CleanupCron { get; init; } = "0 * * * *";
    public TimeSpan PendingExpiration { get; init; } = TimeSpan.FromHours(24);
    public int BatchSize { get; init; } = 100;
    public int MaxFileSizeInMb { get; init; } = 10;
    // defaulted by StorageDependencyInstaller when not configured
    public string[] AllowedContentTypes { get; set; } = [];

    internal long MaxFileSizeInBytes => MaxFileSizeInMb * 1024L * 1024;
}
