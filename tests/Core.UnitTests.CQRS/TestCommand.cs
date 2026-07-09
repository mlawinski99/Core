using Core.CQRS;

namespace Core.UnitTests.CQRS;

public class TestCommand : ICommand<string>
{
    public required string Value { get; init; }
}

public class TestCommandHandler : ICommandHandler<TestCommand, string>
{
    public Task<string> Handle(TestCommand command, CancellationToken cancellationToken)
    {
        return Task.FromResult($"Handled: {command.Value}");
    }
}