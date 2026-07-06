using Core.CQRS;
using Core.CQRS.Decorators;
using Core.Logger;
using Core.ResultPattern;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace Core.UnitTests.CQRS.Decorators;

public class LoggingRequestDecoratorTests
{
    private readonly IAppLogger<LoggingRequestDecorator<TestCommand, Result>> _commandLogger =
        Substitute.For<IAppLogger<LoggingRequestDecorator<TestCommand, Result>>>();

    private readonly IAppLogger<LoggingRequestDecorator<TestQuery, Result<int>>> _queryLogger =
        Substitute.For<IAppLogger<LoggingRequestDecorator<TestQuery, Result<int>>>>();

    [Fact]
    public async Task Handle_ShouldLogBeforeAndAfterExecution()
    {
        var handler = Substitute.For<IRequestHandler<TestCommand, Result>>();
        handler.Handle(Arg.Any<TestCommand>(), Arg.Any<CancellationToken>()).Returns(Result.Success);
        var decorator = new LoggingRequestDecorator<TestCommand, Result>(handler, _commandLogger);

        await decorator.Handle(new TestCommand("test"), CancellationToken.None);

        _commandLogger.Received(2).LogInformation(Arg.Any<string>(), Arg.Any<object[]>());
    }

    [Fact]
    public async Task Handle_WithQuery_ShouldLogBeforeAndAfterExecution()
    {
        var handler = Substitute.For<IRequestHandler<TestQuery, Result<int>>>();
        handler.Handle(Arg.Any<TestQuery>(), Arg.Any<CancellationToken>()).Returns(Result<int>.Success(10));
        var decorator = new LoggingRequestDecorator<TestQuery, Result<int>>(handler, _queryLogger);

        await decorator.Handle(new TestQuery(5), CancellationToken.None);

        _queryLogger.Received(2).LogInformation(Arg.Any<string>(), Arg.Any<object[]>());
    }
}