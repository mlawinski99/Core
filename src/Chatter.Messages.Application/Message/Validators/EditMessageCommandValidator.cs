using Chatter.Messages.Application.Message.Errors;
using Chatter.MessagesDomain;
using FluentValidation;
using static Chatter.Messages.Application.Message.Commands.EditMessage;

namespace Chatter.Messages.Application.Message.Validators;

public class EditMessageCommandValidator : AbstractValidator<EditMessageCommand>
{
    public EditMessageCommandValidator()
    {
        RuleFor(x => x.ChatId)
            .NotEmpty()
            .WithMessage(ValidationMessages.ChatIdRequired);

        RuleFor(x => x.MessageId)
            .NotEmpty()
            .WithMessage(ValidationMessages.MessageIdRequired);

        RuleFor(x => x.Content)
            .NotEmpty()
            .WithMessage(ValidationMessages.MessageContentRequired);

        RuleFor(x => x.Content)
            .MaximumLength(MessageContent.MaxLength)
            .WithMessage(ValidationMessages.MessageContentTooLong);
    }
}
