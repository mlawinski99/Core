using Core.CQRS;

namespace Core.UnitTests.CQRS;

public class ScopeMarker
{
    public Guid Id { get; } = Guid.NewGuid();
}

public class ScopedCommand : ICommand<Guid>;

public class ScopedCommandHandler(ScopeMarker marker) : ICommandHandler<ScopedCommand, Guid>
{
    public Task<Guid> Handle(ScopedCommand command, CancellationToken cancellationToken) => Task.FromResult(marker.Id);
}
