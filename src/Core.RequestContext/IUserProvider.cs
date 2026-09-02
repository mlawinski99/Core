namespace Core.RequestContext;

public interface IUserProvider
{
    Guid? UserId { get; }
}