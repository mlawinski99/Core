using Core.BackgroundJobs;
using FluentAssertions;
using Xunit;

namespace Core.UnitTests.BackgroundJobs;

public class CronValidatorTests
{
    [Theory]
    [InlineData("* * * * *")]
    [InlineData("0 3 * * *")]
    [InlineData("*/5 * * * *")]
    [InlineData("*/30 * * * * *")]
    public void IsValid_SupportedExpression_ReturnsTrue(string cron)
    {
        CronValidator.IsValid(cron).Should().BeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("0 3 * *")]
    [InlineData("0 3 * * * * *")]
    [InlineData("61 * * * *")]
    public void IsValid_UnsupportedExpression_ReturnsFalse(string? cron)
    {
        CronValidator.IsValid(cron).Should().BeFalse();
    }
}
