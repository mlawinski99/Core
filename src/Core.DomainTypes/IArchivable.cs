namespace Core.DomainTypes;

public interface IArchivable
{
    bool IsArchived { get; set; }
    DateTime? DateArchivedUtc { get; set; }
}