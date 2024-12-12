using FluentValidation;

namespace Auth.Application.Account.Commands.ConfirmEmailChange
{
    public class ConfirmEmailChangeCommandValidator : AbstractValidator<ConfirmEmailChangeCommand>
    {
        public ConfirmEmailChangeCommandValidator()
        {
            RuleFor(v => v.Email)
                .NotEmpty().WithMessage("{PropertyName} is required.")
                .MaximumLength(255).WithMessage("{PropertyName} has reached a maximum of {MaxLength} characters.")
                .EmailAddress().WithMessage("{PropertyName} is invalid.");

            RuleFor(v => v.UserId)
                .NotEmpty().WithMessage("{PropertyName} is required.");

            RuleFor(v => v.Code)
                .NotEmpty().WithMessage("{PropertyName} is required.");
        }
    }
}