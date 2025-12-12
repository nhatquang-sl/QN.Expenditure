using FluentValidation;

namespace Auth.Application.Account.Commands.Login
{
    public class LoginCommandValidator : AbstractValidator<LoginCommand>
    {
        private const string ErrorMessage = "Email or Password incorrect.";

        public LoginCommandValidator()
        {
            RuleFor(v => v.Email)
                .NotEmpty().WithMessage(ErrorMessage)
                .MaximumLength(255).WithMessage(ErrorMessage)
                .EmailAddress().WithMessage(ErrorMessage)
                .OverridePropertyName(string.Empty);

            RuleFor(v => v.Password)
                .MinimumLength(6).WithMessage(ErrorMessage)
                .Matches(@"[a-z]+").WithMessage(ErrorMessage)
                .Matches(@"[A-Z]+").WithMessage(ErrorMessage)
                .Matches(@"[0-9]+").WithMessage(ErrorMessage)
                .Matches(@"[!@#$%^&*()_+=\[{\]};:<>|./?,-]+").WithMessage(ErrorMessage)
                .OverridePropertyName(string.Empty);
        }
    }
}