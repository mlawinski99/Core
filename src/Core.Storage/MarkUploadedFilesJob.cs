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

        var files = await db.StoredFiles
            .Where(f => f.Status == StoredFileStatus.Pending && f.DateCreatedUtc >= createdAfterUtc)
            .OrderBy(f => f.DateCreatedUtc)
            .ToListAsync(cancellationToken);

        var markedIds = new List<Guid>();

        try
        {
            foreach (var batch in files.Chunk(_options.BatchSize))
            {
                var batchMarkedIds = new List<Guid>();
                foreach (var file in batch)
                {
                    if ((await fileService.MarkUploaded(file, cancellationToken)).IsSuccess)
                        batchMarkedIds.Add(file.Id);
                }

                await db.SaveChangesAsync(cancellationToken);

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
