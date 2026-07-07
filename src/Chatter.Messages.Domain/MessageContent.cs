using Core.DomainTypes;

namespace Chatter.MessagesDomain;

public class MessageContent : ValueObject
{
    public const int MaxLength = 1000;

    public string Text { get; }

    private MessageContent(string text)
    {
        Text = text;
    }

    public static MessageContent Create(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            throw new DomainException("Message cannot be empty.");

        if (text.Length > MaxLength)
            throw new DomainException("Message is too long.");

        return new MessageContent(text);
    }

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return Text;
    }
}