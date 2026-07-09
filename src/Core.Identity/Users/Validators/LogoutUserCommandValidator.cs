using Core.Identity.Users.Errors;
using FluentValidation;
using static Core.Identity.Users.Commands.LogoutUser;

namespace Core.Identity.Users.Validators;

public class LogoutUserCommandValidator : AbstractValidator<LogoutUserCommand>
{
    public LogoutUserCommandValidator()
    {
        RuleFor(x => x.RefreshToken)
            .NotEmpty()
            .WithMessage(ValidationMessages.RefreshTokenRequired);
    }
}