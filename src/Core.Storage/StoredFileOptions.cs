namespace Core.Storage;

public class StoredFileOptions
{
    public const string SectionName = "StoredFiles";

    // @TODO validate it runs more often than PendingExpiration once a cron extension supports all formats - dont override in config for now
    public string MarkUploadedCron { get; init; } = "* * * * *";
    public string CleanupCron { get; init; } = "0 * * * *";
    public TimeSpan PendingExpiration { get; init; } = TimeSpan.FromHours(24);
    public int BatchSize { get; init; } = 100;
    public int MaxFileSizeInMb { get; init; } = 10;
    // defaulted by StorageDependencyInstaller when not configured
    public string[] AllowedContentTypes { get; set; } = [];

    internal long MaxFileSizeInBytes => MaxFileSizeInMb * 1024L * 1024;
}
