namespace Chatter.Messages.Application.Message.Errors;

public static class ErrorMessages
{
    public const string MessageNotFound = "Message not found";
    public const string ChatNotFound = "Chat not found";
    public const string NotChatMember = "You are not a member of this chat";
    public const string MessageDoesNotBelongToChat = "Message does not belong to the specified chat";
    public const string CanOnlyDeleteOwnMessages = "You can only delete your own messages";
    public const string CanOnlyEditOwnMessages = "You can only edit your own messages";
}
