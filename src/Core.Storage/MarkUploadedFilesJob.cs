using Core.BackgroundJobs;
using Core.BackgroundJobs.Attributes;
using Core.DateTimeProvider;
using Core.Logger;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Core.Storage;

[DisallowConcurrentExecution]
[Retry(0)]
internal class MarkUploadedFilesJob<TContext>(
    TContext db,
    FileService fileService,
    IAppLogger<MarkUploadedFilesJob<TContext>> logger,
    IDateTimeProvider dateTimeProvider,
    IOptions<StoredFileOptions> options)
    : IBackgroundJob
    where TContext : DbContext, IFileStore
{
    public const string JobId = "mark-uploaded-files";

    private readonly StoredFileOptions _options = options.Value;

    public async Task Run(CancellationToken cancellationToken)
    {
        var createdAfterUtc = dateTimeProvider.UtcNow - _options.PendingExpiration;

        var ids = await db.StoredFiles
            .Where(f => f.Status == StoredFileStatus.Pending && f.DateCreatedUtc >= createdAfterUtc)
            .OrderBy(f => f.DateCreatedUtc)
            .Select(f => f.Id)
            .ToListAsync(cancellationToken);

        var markedIds = new List<Guid>();

        try
        {
            foreach (var batchIds in ids.Chunk(_options.BatchSize))
            {
                var batch = await db.StoredFiles
                    .Where(f => batchIds.Contains(f.Id))
                    .ToListAsync(cancellationToken);

                var batchMarkedIds = new List<Guid>();
                foreach (var file in batch)
                {
                    if ((await fileService.MarkUploaded(file, cancellationToken)).IsSuccess)
                        batchMarkedIds.Add(file.Id);
                }

                await db.SaveChangesAsync(cancellationToken);
                db.ChangeTracker.Clear();

                markedIds.AddRange(batchMarkedIds);
            }
        }
        catch (Exception e)
        {
            logger.LogError(e, "Marking stored files as uploaded failed after marking {MarkedCount} files: {MarkedFileIds}",
                markedIds.Count, string.Join(", ", markedIds));
            throw;
        }
        finally
        {
            logger.LogInformation("Marked {MarkedCount} pending stored files as uploaded", markedIds.Count);
        }
    }
}
