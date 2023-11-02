using FluentValidation;

namespace Application.Auth.Commands.Login
{
    public class LoginCommandValidator : AbstractValidator<LoginCommand>
    {
        readonly string errorMessage = "Email or Password incorrect.";
        public LoginCommandValidator()
        {
            RuleFor(v => v.Email)
                .NotEmpty().WithMessage(errorMessage)
                .MaximumLength(255).WithMessage(errorMessage)
                .EmailAddress().WithMessage(errorMessage)
                .OverridePropertyName(string.Empty);

            RuleFor(v => v.Password)
                .MinimumLength(6).WithMessage(errorMessage)
                .Matches(@"[a-z]+").WithMessage(errorMessage)
                .Matches(@"[A-Z]+").WithMessage(errorMessage)
                .Matches(@"[0-9]+").WithMessage(errorMessage)
                .Matches(@"[!@#$%^&*()_+=\[{\]};:<>|./?,-]+").WithMessage(errorMessage)
                .OverridePropertyName(string.Empty);
        }
    }
}
