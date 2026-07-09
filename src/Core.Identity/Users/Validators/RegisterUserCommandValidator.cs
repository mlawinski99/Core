using Core.Identity.Users.Errors;
using FluentValidation;
using static Core.Identity.Users.Commands.RegisterUser;

namespace Core.Identity.Users.Validators;

public class RegisterUserCommandValidator : AbstractValidator<RegisterUserCommand>
{
    public RegisterUserCommandValidator()
    {
        RuleFor(x => x.Username)
            .NotEmpty()
            .WithMessage(ValidationMessages.UsernameRequired);

        RuleFor(x => x.Email)
            .NotEmpty()
            .WithMessage(ValidationMessages.EmailRequired);
            
        RuleFor(x => x.Email)
            .EmailAddress()
            .WithMessage(ValidationMessages.InvalidEmailFormat);

        RuleFor(x => x.Password)
            .NotEmpty()
            .WithMessage(ValidationMessages.PasswordRequired);
        
        RuleFor(x => x.Password)
            .MinimumLength(8)
            .WithMessage(ValidationMessages.PasswordMinLength);
        
        RuleFor(x => x.ConfirmPassword)
            .Equal(x => x.Password)
            .WithMessage(ValidationMessages.PasswordsDoNotMatch);
    }
}