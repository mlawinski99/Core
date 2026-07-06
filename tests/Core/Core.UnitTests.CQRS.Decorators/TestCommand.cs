using Core.CQRS;
using Core.ResultPattern;

namespace Core.UnitTests.CQRS.Decorators;

public record TestCommand(string Value) : ICommand<Result>;

public class TestCommandHandler : ICommandHandler<TestCommand, Result>
{
    public Task<Result> Handle(TestCommand command, CancellationToken cancellationToken)
    {
        return Task.FromResult(Result.Success);
    }
}

public class FailingCommandHandler : ICommandHandler<TestCommand, Result>
{
    public Task<Result> Handle(TestCommand command, CancellationToken cancellationToken)
    {
        throw new InvalidOperationException("Something went wrong");
    }
}