using FluentValidation;

namespace Auth.Application.Account.Commands.ConfirmEmail
{
    public class ConfirmEmailCommandValidator : AbstractValidator<ConfirmEmailCommand>
    {
        public ConfirmEmailCommandValidator()
        {
            RuleFor(v => v.UserId)
                .NotEmpty().WithMessage("UserId is required.");

            RuleFor(v => v.Code)
                .NotEmpty().WithMessage("{PropertyName} is required.");
        }
    }
}