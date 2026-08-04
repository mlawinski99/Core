using Core.CQRS;
using Core.CQRS.Decorators;
using Core.ResultPattern;
using FluentAssertions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Xunit;

namespace Core.UnitTests.CQRS.Decorators;

public class TransactionCommandDecoratorTests
{
    private readonly TestUnitOfWork _unitOfWork = new();

    [Fact]
    public async Task Handle_WhenCommandIsTransactional_ShouldRunThroughUnitOfWork()
    {
        var handler = Substitute.For<IRequestHandler<TestCommand, Result>>();
        handler.Handle(Arg.Any<TestCommand>(), Arg.Any<CancellationToken>()).Returns(Result.Success);
        var decorator = new TransactionCommandDecorator<TestCommand, Result>(handler, _unitOfWork);

        var result = await decorator.Handle(new TestCommand("test"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        _unitOfWork.TransactionUsed.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_WhenNonTransactionalCommandHasActiveTransaction_ShouldThrow()
    {
        _unitOfWork.HasActiveTransaction = true;
        var handler = Substitute.For<IRequestHandler<NonTransactionalTestCommand, Result>>();
        var decorator =
            new TransactionCommandDecorator<NonTransactionalTestCommand, Result>(handler, _unitOfWork);

        var act = () => decorator.Handle(new NonTransactionalTestCommand("test"), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
        await handler.DidNotReceive().Handle(Arg.Any<NonTransactionalTestCommand>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenCommandIsNonTransactional_ShouldBypassUnitOfWork()
    {
        var handler = Substitute.For<IRequestHandler<NonTransactionalTestCommand, Result>>();
        handler.Handle(Arg.Any<NonTransactionalTestCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success);
        var decorator =
            new TransactionCommandDecorator<NonTransactionalTestCommand, Result>(handler, _unitOfWork);

        var result = await decorator.Handle(new NonTransactionalTestCommand("test"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        _unitOfWork.TransactionUsed.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_WhenHandlerThrows_ShouldThrowException()
    {
        var handler = Substitute.For<IRequestHandler<TestCommand, Result>>();
        handler.Handle(Arg.Any<TestCommand>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException());
        var decorator = new TransactionCommandDecorator<TestCommand, Result>(handler, _unitOfWork);

        var act = () => decorator.Handle(new TestCommand("test"), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }
}