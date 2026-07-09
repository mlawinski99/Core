namespace Core.DateTimeProvider;

public interface IDateTimeProvider
{
    DateTime UtcNow { get; }
}