using Chatter.Messages.Application.Message.Errors;
using Chatter.MessagesDomain;
using FluentValidation;
using static Chatter.Messages.Application.Message.Commands.SendMessage;

namespace Chatter.Messages.Application.Message.Validators;

public class SendMessageCommandValidator : AbstractValidator<SendMessageCommand>
{
    public SendMessageCommandValidator()
    {
        RuleFor(x => x.ChatId)
            .NotEmpty()
            .WithMessage(ValidationMessages.ChatIdRequired);

        RuleFor(x => x.Content)
            .NotEmpty()
            .WithMessage(ValidationMessages.MessageContentRequired);

        RuleFor(x => x.Content)
            .MaximumLength(MessageContent.MaxLength)
            .WithMessage(ValidationMessages.MessageContentTooLong);
    }
}
