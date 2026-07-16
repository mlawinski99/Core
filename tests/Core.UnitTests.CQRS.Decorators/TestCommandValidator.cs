using FluentValidation;

namespace Core.UnitTests.CQRS.Decorators;

public class TestCommandValidator : AbstractValidator<TestCommand>
{
    public TestCommandValidator()
    {
        RuleFor(x => x.Value).NotEmpty().WithMessage("Value is required");
    }
}
