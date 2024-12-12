using FluentValidation;

namespace Auth.Application.Account.Commands.ChangeEmail
{
    public class ChangeEmailCommandValidator : AbstractValidator<ChangeEmailCommand>
    {
        public ChangeEmailCommandValidator()
        {
            RuleFor(v => v.NewEmail)
                .NotEmpty().WithMessage("{PropertyName} is required.")
                .MaximumLength(255).WithMessage("{PropertyName} has reached a maximum of {MaxLength} characters.")
                .EmailAddress().WithMessage("{PropertyName} is invalid.");
        }
    }
}