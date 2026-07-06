using Core.CQRS;
using Core.CQRS.Decorators;
using Core.ResultPattern;
using Core.Validation;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace Core.UnitTests.CQRS.Decorators;

public class ValidationRequestDecoratorTests
{
    private readonly IRequestHandler<TestCommand, Result> _handler = Substitute.For<IRequestHandler<TestCommand, Result>>();

    public ValidationRequestDecoratorTests()
    {
        _handler.Handle(Arg.Any<TestCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success);
    }

    [Fact]
    public async Task Handle_WhenValidationPasses_ShouldCallHandler()
    {
        var validator = new FluentValidationAdapter<TestCommand>([new TestCommandValidator()]);
        var decorator = new ValidationRequestDecorator<TestCommand, Result>(_handler, [validator]);

        var result = await decorator.Handle(new TestCommand("valid"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        await _handler.Received(1).Handle(Arg.Any<TestCommand>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenValidationFails_ShouldReturnBadRequest()
    {
        var validator = new FluentValidationAdapter<TestCommand>([new TestCommandValidator()]);
        var decorator = new ValidationRequestDecorator<TestCommand, Result>(_handler, [validator]);

        var result = await decorator.Handle(new TestCommand(""), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Code.Should().Be(ResultCode.BadRequest);
        result.Error.Should().Contain("Value is required");
        await _handler.DidNotReceive().Handle(Arg.Any<TestCommand>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenNoValidatorsRegistered_ShouldCallHandler()
    {
        var decorator = new ValidationRequestDecorator<TestCommand, Result>(_handler, []);

        var result = await decorator.Handle(new TestCommand("test"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        await _handler.Received(1).Handle(Arg.Any<TestCommand>(), Arg.Any<CancellationToken>());
    }
}