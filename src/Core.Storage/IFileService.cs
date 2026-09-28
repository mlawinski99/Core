using Core.ResultPattern;

namespace Core.Storage;

public interface IFileService
{
    Result<FileUpload> CreateUpload(string fileName, string contentType);

    Task<Result> Confirm(Guid fileId, CancellationToken cancellationToken = default);

    Task<Result<PresignedUrl>> GetDownloadUrl(Guid fileId, CancellationToken cancellationToken = default);

    Task<Result> Delete(Guid fileId, CancellationToken cancellationToken = default);
}