using Core.DomainTypes;

namespace Chatter.MessagesDomain.Events;

public class MessageEdited : DomainEventBase
{
    public Guid MessageId { get; private set; }
    public Guid ChatId { get; private set; }

    public MessageEdited(Guid messageId, Guid chatId)
    {
        MessageId = messageId;
        ChatId = chatId;
    }
}