namespace Chatter.Messages.Application.Message.Errors;

public static class ValidationMessages
{
    public const string ChatIdRequired = "ChatId is required";
    public const string MessageIdRequired = "MessageId is required";
    public const string MessageContentRequired = "Message cannot be empty.";
    public const string MessageContentTooLong = "Message is too long.";
}
