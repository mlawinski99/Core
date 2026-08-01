using Core.CQRS;

namespace Core.UnitTests.CQRS;

public class InternalHandlerCommand : ICommand<string>
{
    public required string Value { get; init; }
}

internal class InternalHandlerCommandHandler : ICommandHandler<InternalHandlerCommand, string>
{
    public Task<string> Handle(InternalHandlerCommand command, CancellationToken cancellationToken) =>
        Task.FromResult(command.Value);
}
