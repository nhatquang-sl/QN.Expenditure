using FluentValidation;

namespace Auth.Application.Account.Commands.ResetPassword
{
    public class ResetPasswordCommandValidator : AbstractValidator<ResetPasswordCommand>
    {
        public ResetPasswordCommandValidator()
        {
            RuleFor(v => v.Email)
                .NotEmpty().WithMessage("{PropertyName} is required.")
                .MaximumLength(255).WithMessage("{PropertyName} has reached a maximum of {MaxLength} characters.")
                .EmailAddress().WithMessage("{PropertyName} is invalid.");

            RuleFor(v => v.Password)
                .NotEmpty().WithMessage("{PropertyName} is required.")
                .MinimumLength(6).WithMessage("{PropertyName} must be at least {MinLength} characters.")
                .Matches(@"[a-z]+").WithMessage("{PropertyName} must have at least one lowercase ('a'-'z').")
                .Matches(@"[A-Z]+").WithMessage("{PropertyName} must have at least one uppercase ('A'-'Z').")
                .Matches(@"[0-9]+").WithMessage("{PropertyName} must contain at least one number.")
                .Matches(@"[!@#$%^&*()_+=\[{\]};:<>|./?,-]+")
                .WithMessage("{PropertyName} must have at least one non alphanumeric character.");

            RuleFor(v => v.ConfirmPassword)
                .Must((model, confirmPassword) => confirmPassword == model.Password)
                .When(x => !string.IsNullOrWhiteSpace(x.Password))
                .WithMessage("{PropertyName} and New Password do not match.");

            RuleFor(v => v.Code)
                .NotEmpty().WithMessage("{PropertyName} is required.");
        }
    }
}