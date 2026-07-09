namespace Core.DomainTypes;

public interface ISoftDeletable
{
    DateTime? DateDeletedUtc { get; set; }
    bool IsDeleted { get; set; }
}