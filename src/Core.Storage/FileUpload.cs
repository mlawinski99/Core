namespace Core.Storage;

public record FileUpload(Guid FileId, PresignedUpload Upload);