using Core.CQRS;
using Core.Outbox.Models;
using Core.Pager;
using Core.ResultPattern;
using Microsoft.EntityFrameworkCore;

using static Core.Outbox.Errors.ErrorMessages;

namespace Core.Outbox.Queries;

public record GetDeadLettersQuery(int Page, int PageSize) : IQuery<Result<PagedResult<DeadLetterListItem>>>;

internal sealed class GetDeadLetters(IOutbox outbox)
    : IQueryHandler<GetDeadLettersQuery, Result<PagedResult<DeadLetterListItem>>>
{
    public async Task<Result<PagedResult<DeadLetterListItem>>> Handle(GetDeadLettersQuery query, CancellationToken cancellationToken)
    {
        // @TODO any query decorator for this check?
        if (query.Page < 1 || query.PageSize < 1)
            return Result<PagedResult<DeadLetterListItem>>.BadRequest(InvalidPage);

        var totalCount = await outbox.DeadLetterOutboxMessages.CountAsync(cancellationToken);

        var items = await outbox.DeadLetterOutboxMessages
            .AsNoTracking()
            .OrderByDescending(d => d.DeadLetteredOnUtc)
            .ThenBy(d => d.Id)
            .Paginate(query.Page, query.PageSize)
            .Select(d => new DeadLetterListItem(d.Id, d.Type, d.AggregateId, d.IsBlocking, d.RetryCount, d.LastError, d.DeadLetteredOnUtc))
            .ToListAsync(cancellationToken);

        var page = PagedResult<DeadLetterListItem>.Create(items, query.Page, PagerSettings.Normalize(query.PageSize), totalCount);

        return Result<PagedResult<DeadLetterListItem>>.Success(page);
    }
}
