using Core.DomainTypes;

namespace Chatter.MessagesDomain.Events;

public class MessageCreated : DomainEventBase
{
    public Guid MessageId { get; private set; }
    public Guid ChatId { get; private set; }
    public Guid AuthorId { get; private set; }

    public MessageCreated(Guid messageId, Guid chatId, Guid authorId)
    {
        MessageId = messageId;
        ChatId = chatId;
        AuthorId = authorId;
    }
}