namespace Core.DomainTypes;

public interface IVersionable
{
    int VersionId { get; set; }
    Guid? VersionGroupId { get; set; } // id of 1st version
}