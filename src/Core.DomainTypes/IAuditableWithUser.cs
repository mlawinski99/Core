namespace Core.DomainTypes;

public interface IAuditableWithUser : IAuditable
{
    Guid? CreatedBy { get; set; }
    Guid? ModifiedBy { get; set; }
}