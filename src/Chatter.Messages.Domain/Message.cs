using Chatter.MessagesDomain.Events;
using Chatter.Shared.Domain;
using Chatter.Shared.DomainTypes;
using Core.DomainTypes;

namespace Chatter.MessagesDomain;

public class Message : AggregateRoot, IAuditableWithUser, ISoftDeletable, IVersionable
{
    public DateTime DateCreatedUtc { get; set; }
    public DateTime? DateModifiedUtc { get; set; }
    public Guid? CreatedBy { get; set; }
    public Guid? ModifiedBy { get; set; }
    public MessageStatus Status { get; set; }
    public MessageContent Content { get; set; }
    public DateTime? DateDeletedUtc { get; set; }
    public bool IsDeleted { get; set; }
    public int VersionId { get; set; }
    public Guid? VersionGroupId { get; set; }
    public Guid ChatId { get; set; }
    public Chat Chat { get; set; }
    public Guid SenderId { get; set; }
    public User Sender { get; set; }
    
    public static Message Create(MessageContent content, Guid senderId, Guid chatId)
    {
        var message = new Message();
        message.Status = MessageStatus.Sending;
        message.Content = content;
        message.SenderId = senderId;
        message.ChatId = chatId;
        message.AddDomainEvent(new MessageCreated(message.Id, chatId, senderId));

        return message;
    }

    public void Delete()
    {
        Status = MessageStatus.Deleted;
        AddDomainEvent(new MessageDeleted(Id, ChatId));
    }

    public void Edit(MessageContent newContent)
    {
        Content = newContent;
        Status = MessageStatus.Edited;
        AddDomainEvent(new MessageEdited(Id, ChatId));
    }
}