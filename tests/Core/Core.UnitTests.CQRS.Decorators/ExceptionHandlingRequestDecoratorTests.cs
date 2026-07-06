using Core.CQRS;
using Core.CQRS.Decorators;
using Core.DomainTypes;
using Core.Logger;
using Core.ResultPattern;
using FluentAssertions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Xunit;

namespace Core.UnitTests.CQRS.Decorators;

public class ExceptionHandlingRequestDecoratorTests
{
    private readonly IAppLogger<ExceptionHandlingRequestDecorator<TestCommand, Result>> _commandLogger =
        Substitute.For<IAppLogger<ExceptionHandlingRequestDecorator<TestCommand, Result>>>();

    private readonly IAppLogger<ExceptionHandlingRequestDecorator<TestQuery, Result<int>>> _queryLogger =
        Substitute.For<IAppLogger<ExceptionHandlingRequestDecorator<TestQuery, Result<int>>>>();

    [Fact]
    public async Task Handle_WhenCommandSucceeds_ShouldReturnResult()
    {
        var handler = Substitute.For<IRequestHandler<TestCommand, Result>>();
        handler.Handle(Arg.Any<TestCommand>(), Arg.Any<CancellationToken>()).Returns(Result.Success);
        var decorator = new ExceptionHandlingRequestDecorator<TestCommand, Result>(handler, _commandLogger);

        var result = await decorator.Handle(new TestCommand("test"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_WhenCommandThrows_ShouldReturnInternalError()
    {
        var handler = Substitute.For<IRequestHandler<TestCommand, Result>>();
        handler.Handle(Arg.Any<TestCommand>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("test"));
        var decorator = new ExceptionHandlingRequestDecorator<TestCommand, Result>(handler, _commandLogger);

        var result = await decorator.Handle(new TestCommand("test"), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Code.Should().Be(ResultCode.InternalError);
        result.Error.Should().Be("Something went wrong");
    }

    [Fact]
    public async Task Handle_WhenCommandThrows_ShouldLogException()
    {
        var handler = Substitute.For<IRequestHandler<TestCommand, Result>>();
        var exception = new InvalidOperationException("test exception");
        handler.Handle(Arg.Any<TestCommand>(), Arg.Any<CancellationToken>()).ThrowsAsync(exception);
        var decorator = new ExceptionHandlingRequestDecorator<TestCommand, Result>(handler, _commandLogger);

        await decorator.Handle(new TestCommand("test"), CancellationToken.None);

        _commandLogger.Received(1).LogError(exception, Arg.Any<string>(), Arg.Any<object[]>());
    }

    [Fact]
    public async Task Handle_WhenDomainExceptionThrown_ShouldReturnUnprocessableEntity()
    {
        var handler = Substitute.For<IRequestHandler<TestCommand, Result>>();
        handler.Handle(Arg.Any<TestCommand>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new DomainException("test"));
        var decorator = new ExceptionHandlingRequestDecorator<TestCommand, Result>(handler, _commandLogger);

        var result = await decorator.Handle(new TestCommand("test"), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Code.Should().Be(ResultCode.UnprocessableEntity);
        result.Error.Should().Be("test");
    }

    [Fact]
    public async Task Handle_WhenDomainExceptionThrown_ShouldLogWarning()
    {
        var handler = Substitute.For<IRequestHandler<TestCommand, Result>>();
        handler.Handle(Arg.Any<TestCommand>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new DomainException("test"));
        var decorator = new ExceptionHandlingRequestDecorator<TestCommand, Result>(handler, _commandLogger);

        await decorator.Handle(new TestCommand("test"), CancellationToken.None);

        _commandLogger.Received(1).LogWarning(Arg.Any<string>(), Arg.Any<object[]>());
        _commandLogger.DidNotReceive().LogError(Arg.Any<Exception>(), Arg.Any<string>(), Arg.Any<object[]>());
    }

    [Fact]
    public async Task Handle_WhenCancelled_ShouldPropagateOperationCanceledException()
    {
        var handler = Substitute.For<IRequestHandler<TestCommand, Result>>();
        handler.Handle(Arg.Any<TestCommand>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new OperationCanceledException());
        var decorator = new ExceptionHandlingRequestDecorator<TestCommand, Result>(handler, _commandLogger);
        var cancelledToken = new CancellationToken(canceled: true);

        var act = () => decorator.Handle(new TestCommand("test"), cancelledToken);

        await act.Should().ThrowAsync<OperationCanceledException>();
        _commandLogger.Received(1).LogInformation(Arg.Any<string>(), Arg.Any<object[]>());
        _commandLogger.DidNotReceive().LogError(Arg.Any<Exception>(), Arg.Any<string>(), Arg.Any<object[]>());
    }

    [Fact]
    public async Task Handle_WhenOperationCanceledButTokenNotCancelled_ShouldReturnInternalError()
    {
        var handler = Substitute.For<IRequestHandler<TestCommand, Result>>();
        handler.Handle(Arg.Any<TestCommand>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new OperationCanceledException());
        var decorator = new ExceptionHandlingRequestDecorator<TestCommand, Result>(handler, _commandLogger);

        var result = await decorator.Handle(new TestCommand("test"), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Code.Should().Be(ResultCode.InternalError);
        _commandLogger.Received(1).LogError(Arg.Any<Exception>(), Arg.Any<string>(), Arg.Any<object[]>());
        _commandLogger.DidNotReceive().LogInformation(Arg.Any<string>(), Arg.Any<object[]>());
    }

    [Fact]
    public async Task Handle_WhenQueryThrows_ShouldReturnInternalError()
    {
        var handler = Substitute.For<IRequestHandler<TestQuery, Result<int>>>();
        handler.Handle(Arg.Any<TestQuery>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("test"));
        var decorator = new ExceptionHandlingRequestDecorator<TestQuery, Result<int>>(handler, _queryLogger);

        var result = await decorator.Handle(new TestQuery(5), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Code.Should().Be(ResultCode.InternalError);
        result.Error.Should().Be("Something went wrong");
    }
}