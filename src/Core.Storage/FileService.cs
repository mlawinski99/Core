using Core.DateTimeProvider;
using Core.RequestContext;
using Core.ResultPattern;
using Core.Storage.Errors;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Core.Storage;

internal class FileService(
    IFileStore fileStore,
    IStorageService storageService,
    IDateTimeProvider dateTimeProvider,
    IUserProvider userProvider,
    IOptions<StoredFileOptions> options)
    : IFileService
{
    private readonly StoredFileOptions _options = options.Value;

    public Result<FileUpload> CreateUpload(string fileName, string contentType)
    {
        if (!_options.AllowedContentTypes.Contains(contentType, StringComparer.OrdinalIgnoreCase))
            return Result<FileUpload>.BadRequest(ErrorMessages.ContentTypeNotAllowed);

        var id = Guid.NewGuid();
        var file = new StoredFile
        {
            Id = id,
            Key = Guid.NewGuid().ToString(),
            FileName = fileName,
            ContentType = contentType,
            Status = StoredFileStatus.Pending
        };
        fileStore.StoredFiles.Add(file);

        var presignedUpload = storageService.CreatePresignedUpload(file.Key, contentType);

        return Result<FileUpload>.Success(new FileUpload(id, presignedUpload));
    }

    public async Task<Result> Confirm(Guid fileId, CancellationToken cancellationToken = default)
    {
        if (userProvider.UserId is null)
            return Result.NotFound(ErrorMessages.FileNotFound);

        var file = await fileStore.StoredFiles
            .FirstOrDefaultAsync(f => f.Id == fileId && f.CreatedBy == userProvider.UserId, cancellationToken);
        if (file is null)
            return Result.NotFound(ErrorMessages.FileNotFound);

        return file.Status == StoredFileStatus.Pending
            ? await MarkUploaded(file, cancellationToken)
            : Result.Success;
    }

    // @TODO permissions check
    public async Task<Result<PresignedUrl>> GetDownloadUrl(Guid fileId, CancellationToken cancellationToken = default)
    {
        var key = await fileStore.StoredFiles
            .Where(f => f.Id == fileId && f.Status == StoredFileStatus.Uploaded)
            .Select(f => f.Key)
            .FirstOrDefaultAsync(cancellationToken);

        if (key is null)
            return Result<PresignedUrl>.NotFound(ErrorMessages.FileNotFound);

        var url = storageService.GetDownloadUrl(key);
        return Result<PresignedUrl>.Success(url);
    }

    // soft delete; the object is removed from storage by CleanupStoredFilesJob
    // @TODO == userId || hasPermission in future
    public async Task<Result> Delete(Guid fileId, CancellationToken cancellationToken = default)
    {
        if (userProvider.UserId is null)
            return Result.NotFound(ErrorMessages.FileNotFound);

        var file = await fileStore.StoredFiles
            .FirstOrDefaultAsync(f => f.Id == fileId && f.CreatedBy == userProvider.UserId, cancellationToken);
        if (file is null)
            return Result.NotFound(ErrorMessages.FileNotFound);

        fileStore.StoredFiles.Remove(file);

        return Result.Success;
    }

    internal async Task<Result> MarkUploaded(StoredFile file, CancellationToken cancellationToken)
    {
        if (file.DateCreatedUtc < dateTimeProvider.UtcNow - _options.PendingExpiration)
            return Result.UnprocessableEntity(ErrorMessages.FileNotUploaded);

        var metadata = await storageService.GetMetadata(file.Key, cancellationToken);
        if (metadata is null)
            return Result.UnprocessableEntity(ErrorMessages.FileNotUploaded);

        file.SizeInBytes = metadata.Size;
        file.Status = StoredFileStatus.Uploaded;

        return Result.Success;
    }
}
