using Core.CQRS;
using Core.ResultPattern;
using Microsoft.EntityFrameworkCore;

using static Core.Outbox.Errors.ErrorMessages;

namespace Core.Outbox.Commands;

public record ReplayDeadLetterCommand(Guid Id) : ICommand<Result>;

internal sealed class ReplayDeadLetter(IOutbox outbox) : ICommandHandler<ReplayDeadLetterCommand, Result>
{
    public async Task<Result> Handle(ReplayDeadLetterCommand command, CancellationToken cancellationToken)
    {
        var deadLetter = await outbox.DeadLetterOutboxMessages.FirstOrDefaultAsync(d => d.Id == command.Id, cancellationToken);

        if (deadLetter is null)
            return Result.NotFound(DeadLetterNotFound);

        outbox.OutboxMessages.Add(OutboxMapper.ToOutboxMessage(deadLetter));
        outbox.DeadLetterOutboxMessages.Remove(deadLetter);

        return Result.Success;
    }
}
