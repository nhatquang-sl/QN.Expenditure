using Cex.Application.BnbSpotGrid.Commands.CreateSpotGrid;
using FluentValidation;

namespace Application.BnbSpotGrid.Commands.CreateSpotGrid
{
    public class CreateSpotGridCommandValidator : AbstractValidator<CreateSpotGridCommand>
    {
        public CreateSpotGridCommandValidator()
        {
            RuleFor(v => v.Symbol)
                .MinimumLength(5).WithMessage("{PropertyName} must be at least {MinLength} characters.")
                .MaximumLength(10).WithMessage("{PropertyName} has reached a maximum of {MaxLength} characters.");

            RuleFor(v => v.UpperPrice)
                .GreaterThan(x => x.LowerPrice).WithMessage("{PropertyName} must be greater than Lower Price.");

            RuleFor(v => v.TakeProfit)
                .GreaterThan(x => x.StopLoss).WithMessage("{PropertyName} must be greater than Stop Loss.");
        }
    }
}