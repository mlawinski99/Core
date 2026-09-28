using Core.BackgroundJobs;
using Core.BackgroundJobs.Attributes;
using Core.DateTimeProvider;
using Core.Logger;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Core.Storage;

[DisallowConcurrentExecution]
[Retry(0)]
internal class CleanupStoredFilesJob<TContext>(
    TContext db,
    IStorageService storageService,
    IAppLogger<CleanupStoredFilesJob<TContext>> logger,
    IDateTimeProvider dateTimeProvider,
    IOptions<StoredFileOptions> options) : IBackgroundJob where TContext : DbContext, IFileStore
{
    public const string JobId = "cleanup-stored-files";

    // marking and cleanup cant act on the same pending file - need delay
    private static readonly TimeSpan PendingCleanupDelay = TimeSpan.FromHours(1);

    private readonly StoredFileOptions _options = options.Value;

    public async Task Run(CancellationToken cancellationToken)
    {
        var pendingCreatedBeforeUtc = dateTimeProvider.UtcNow - _options.PendingExpiration - PendingCleanupDelay;

        var files = await db.StoredFiles
            .IgnoreQueryFilters()
            .Where(f => f.IsDeleted
                        || (f.Status == StoredFileStatus.Pending && f.DateCreatedUtc < pendingCreatedBeforeUtc))
            .OrderBy(f => f.DateCreatedUtc)
            .Select(f => new { f.Id, f.Key })
            .ToListAsync(cancellationToken);

        var deletedIds = new List<Guid>();

        try
        {
            foreach (var batch in files.Chunk(_options.BatchSize))
            {
                // storage first - failed batch will be retried in next run
                await storageService.Delete([.. batch.Select(f => f.Key)], cancellationToken);

                var ids = batch.Select(f => f.Id).ToList();

                await db.StoredFiles
                    .IgnoreQueryFilters()
                    .Where(f => ids.Contains(f.Id))
                    .ExecuteDeleteAsync(cancellationToken);

                deletedIds.AddRange(ids);
            }
        }
        catch (Exception e)
        {
            logger.LogError(e, "Stored files cleanup failed after deleting {DeletedCount} files: {DeletedFileIds}",
                deletedIds.Count, string.Join(", ", deletedIds));
            throw;
        }
        finally
        {
            logger.LogInformation(
                "Deleted {DeletedCount} stored files that were deleted or pending since before {PendingCreatedBeforeUtc}",
                deletedIds.Count, pendingCreatedBeforeUtc);
        }
    }
}
