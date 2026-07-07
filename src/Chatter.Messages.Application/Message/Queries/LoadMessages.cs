using Core.Infrastructure;
using Chatter.Messages.Application.Message.Errors;
using Chatter.MessagesDataAccess.DbContexts;
using Core.CQRS;
using Core.Pager;
using Core.ResultPattern;
using Microsoft.EntityFrameworkCore;

namespace Chatter.Messages.Application.Message.Queries;

public class LoadMessages : IQueryHandler<LoadMessages.LoadMessagesQuery, Result<CursorPagedResult<LoadMessages.MessageDto>>>
{
    public record MessageDto(Guid Id, string Content, Guid SenderId, string Status, DateTime DateCreatedUtc, DateTime? DateModifiedUtc);
    public record LoadMessagesQuery(Guid ChatId, Guid? LastMessageId = null, int PageSize = 20) : IQuery<Result<CursorPagedResult<MessageDto>>>;

    private readonly ChatDbContext _chatDbContext;
    private readonly IUserProvider _userProvider;

    public LoadMessages(ChatDbContext chatDbContext, IUserProvider userProvider)
    {
        _chatDbContext = chatDbContext;
        _userProvider = userProvider;
    }

    public async Task<Result<CursorPagedResult<MessageDto>>> Handle(LoadMessagesQuery query, CancellationToken cancellationToken)
    {
        var chatExists = await _chatDbContext.Chats
            .AnyAsync(x => x.Id == query.ChatId, cancellationToken);

        if (!chatExists)
            return Result<CursorPagedResult<MessageDto>>.NotFound(ErrorMessages.ChatNotFound);

        var isMember = await _chatDbContext.ChatMembers
            .AnyAsync(x => x.Chat.Id == query.ChatId && x.User.Id == _userProvider.UserId, cancellationToken);

        if (!isMember)
            return Result<CursorPagedResult<MessageDto>>.Forbidden(ErrorMessages.NotChatMember);

        var filteredQuery = _chatDbContext.Messages
            .AsNoTracking()
            .Where(m => m.ChatId == query.ChatId)
            .Where(m => !query.LastMessageId.HasValue ||
                m.DateCreatedUtc < _chatDbContext.Messages
                    .Where(last => last.Id == query.LastMessageId.Value)
                    .Select(last => last.DateCreatedUtc)
                    .First());

        var totalRemaining = await filteredQuery.CountAsync(cancellationToken);

        var messages = await filteredQuery
            .OrderByDescending(m => m.DateCreatedUtc)
            .TakePage(query.PageSize)
            .Select(m => new MessageDto(
                m.Id,
                m.Content.Text,
                m.SenderId,
                m.Status.Name,
                m.DateCreatedUtc,
                m.DateModifiedUtc))
            .ToListAsync(cancellationToken);

        var hasMore = totalRemaining > messages.Count;

        return Result<CursorPagedResult<MessageDto>>.Success(
            new CursorPagedResult<MessageDto>(messages, hasMore));
    }
}
