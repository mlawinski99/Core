using Core.CQRS;
using Core.DateTimeProvider;
using Core.ResultPattern;
using Microsoft.EntityFrameworkCore;

using static Core.Outbox.Errors.ErrorMessages;

namespace Core.Outbox.Commands;

public record DiscardDeadLetterCommand(Guid Id) : ICommand<Result>;

internal sealed class DiscardDeadLetter(IOutbox outbox, IDateTimeProvider dateTimeProvider)
    : ICommandHandler<DiscardDeadLetterCommand, Result>
{
    public async Task<Result> Handle(DiscardDeadLetterCommand command, CancellationToken cancellationToken)
    {
        var deadLetter = await outbox.DeadLetterOutboxMessages.FirstOrDefaultAsync(d => d.Id == command.Id, cancellationToken);

        if (deadLetter is null)
            return Result.NotFound(DeadLetterNotFound);

        outbox.ProcessedOutboxMessages.Add(OutboxMapper.ToDiscarded(deadLetter, dateTimeProvider.UtcNow));
        outbox.DeadLetterOutboxMessages.Remove(deadLetter);

        return Result.Success;
    }
}