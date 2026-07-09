using Core.Identity.Users.Errors;
using FluentValidation;
using static Core.Identity.Users.Commands.LoginUser;

namespace Core.Identity.Users.Validators;

public class LoginUserCommandValidator : AbstractValidator<LoginUserCommand>
{
    public LoginUserCommandValidator()
    {
        RuleFor(x => x.Username)
            .NotEmpty()
            .WithMessage(ValidationMessages.UsernameRequired);
        
        RuleFor(x => x.Password)
            .NotEmpty()
            .WithMessage(ValidationMessages.PasswordRequired);
    }
}