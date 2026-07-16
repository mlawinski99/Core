using Core.DataAccessTypes;
using Core.DateTimeProvider;

namespace Core.IntegrationTests.Shared.Infrastructure;

public class TestDateTimeProvider : IDateTimeProvider
{
    public DateTime UtcNow { get; set; } = new(2025, 1, 20, 12, 0, 0, DateTimeKind.Utc);
}
