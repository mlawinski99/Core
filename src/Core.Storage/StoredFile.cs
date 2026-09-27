using Core.DomainTypes;

namespace Core.Storage;

public sealed class StoredFile : IAuditableWithUser, ISoftDeletable
{
    public Guid Id { get; set; }
    public string Key { get; set; }
    public string FileName { get; set; }
    public string ContentType { get; set; }
    public long? SizeInBytes { get; set; }
    public StoredFileStatus Status { get; set; }
    public DateTime DateCreatedUtc { get; set; }
    public DateTime? DateModifiedUtc { get; set; }
    public Guid? CreatedBy { get; set; }
    public Guid? ModifiedBy { get; set; }
    public DateTime? DateDeletedUtc { get; set; }
    public bool IsDeleted { get; set; }
}