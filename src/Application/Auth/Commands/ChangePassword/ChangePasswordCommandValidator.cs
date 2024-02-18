using FluentValidation;

namespace Application.Auth.Commands.ChangePassword
{
    public class ChangePasswordCommandValidator : AbstractValidator<ChangePasswordCommand>
    {
        public ChangePasswordCommandValidator()
        {
            RuleFor(v => v.OldPassword)
                .NotEmpty().WithMessage("{PropertyName} is required.");

            RuleFor(v => v.NewPassword)
                .NotEmpty().WithMessage("{PropertyName} is required.")
                .MinimumLength(6).WithMessage("{PropertyName} must be at least {MinLength} characters.")
                .Matches(@"[a-z]+").WithMessage("{PropertyName} must have at least one lowercase ('a'-'z').")
                .Matches(@"[A-Z]+").WithMessage("{PropertyName} must have at least one uppercase ('A'-'Z').")
                .Matches(@"[0-9]+").WithMessage("{PropertyName} must contain at least one number.")
                .Matches(@"[!@#$%^&*()_+=\[{\]};:<>|./?,-]+").WithMessage("{PropertyName} must have at least one non alphanumeric character.")
                .OverridePropertyName("Password");

            RuleFor(v => v.ConfirmPassword)
                .Must((model, confirmPassword) => confirmPassword == model.NewPassword)
                .When(x => !string.IsNullOrWhiteSpace(x.NewPassword))
                .WithMessage("{PropertyName} and New Password do not match.");
        }
    }
}
