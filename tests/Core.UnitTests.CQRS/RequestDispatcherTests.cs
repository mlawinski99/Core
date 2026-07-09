using Core.CQRS;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Core.UnitTests.CQRS;

public class RequestDispatcherTests
{
    [Fact]
    public async Task Dispatch_WithCommand_ShouldResolveHandlerAndReturnResult()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddScoped<IRequestHandler<TestCommand, string>, TestCommandHandler>();
        var serviceProvider = services.BuildServiceProvider();
        var dispatcher = new RequestDispatcher(serviceProvider);

        var command = new TestCommand { Value = "test" };

        // Act
        var result = await dispatcher.Dispatch(command);

        // Assert
        result.Should().Be("Handled: test");
    }

    [Fact]
    public async Task Dispatch_WithQuery_ShouldResolveHandlerAndReturnResult()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddScoped<IRequestHandler<TestQuery, int>, TestQueryHandler>();
        var serviceProvider = services.BuildServiceProvider();
        var dispatcher = new RequestDispatcher(serviceProvider);

        var query = new TestQuery { Number = 5 };

        // Act
        var result = await dispatcher.Dispatch(query);

        // Assert
        result.Should().Be(10);
    }

    [Fact]
    public async Task Dispatch_WithUnregisteredHandler_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var services = new ServiceCollection();
        var serviceProvider = services.BuildServiceProvider();
        var dispatcher = new RequestDispatcher(serviceProvider);

        var command = new TestCommand { Value = "test" };

        // Act
        var act = () => dispatcher.Dispatch(command);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task Dispatch_ShouldPassCancellationToken()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddScoped<IRequestHandler<CancellableCommand, bool>, CancellableCommandHandler>();
        var serviceProvider = services.BuildServiceProvider();
        var dispatcher = new RequestDispatcher(serviceProvider);

        var cts = new CancellationTokenSource();
        var command = new CancellableCommand();

        // Act
        var result = await dispatcher.Dispatch(command, cts.Token);

        // Assert
        result.Should().BeTrue();
    }
}