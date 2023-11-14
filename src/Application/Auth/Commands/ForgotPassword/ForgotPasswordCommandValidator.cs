using FluentValidation;

namespace Application.Auth.Commands.ForgotPassword
{
    public class ForgotPasswordCommandValidator : AbstractValidator<ForgotPasswordCommand>
    {
        public ForgotPasswordCommandValidator()
        {
            RuleFor(v => v.Email)
                .NotEmpty().WithMessage("{PropertyName} is required.")
                .MaximumLength(255).WithMessage("{PropertyName} has reached a maximum of {MaxLength} characters.")
                .EmailAddress().WithMessage("{PropertyName} is invalid.");
        }
    }
}
