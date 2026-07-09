namespace Core.DomainTypes;

public interface IAuditable
{
    DateTime DateCreatedUtc { get; set; }
    DateTime? DateModifiedUtc { get; set; }
}