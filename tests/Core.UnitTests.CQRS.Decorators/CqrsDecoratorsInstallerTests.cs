using Core.CQRS;
using Core.CQRS.Decorators;
using Core.DataAccessTypes;
using Core.Logger;
using Core.ResultPattern;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Core.UnitTests.CQRS.Decorators;

public class CqrsDecoratorsInstallerTests
{
    [Fact]
    public void CommandHandler_ShouldResolveThroughDecoratorChain()
    {
        using var scope = BuildProvider().CreateScope();

        var handler = scope.ServiceProvider.GetRequiredService<ICommandHandler<TestCommand, Result>>();

        handler.Should().BeOfType<LoggingCommandDecorator<TestCommand, Result>>();
    }

    [Fact]
    public void QueryHandler_ShouldResolveThroughDecoratorChain()
    {
        using var scope = BuildProvider().CreateScope();

        var handler = scope.ServiceProvider.GetRequiredService<IQueryHandler<TestQuery, Result<int>>>();

        handler.Should().BeOfType<LoggingQueryDecorator<TestQuery, Result<int>>>();
    }

    private static ServiceProvider BuildProvider()
    {
        var services = new ServiceCollection();
        services.AddSingleton(typeof(IAppLogger<>), typeof(TestLogger<>));
        services.AddScoped<IUnitOfWork, TestUnitOfWork>();
        services.AddCqrs(typeof(TestCommandHandler).Assembly);
        services.AddCqrsDecorators();

        return services.BuildServiceProvider();
    }
}

public class TestLogger<T> : IAppLogger<T>
{
    public void LogInformation(string message, params object[] args) { }
    public void LogWarning(string message, params object[] args) { }
    public void LogDebug(string message, params object[] args) { }
    public void LogError(string message, params object[] args) { }
    public void LogError(Exception exception, string message, params object[] args) { }
}
