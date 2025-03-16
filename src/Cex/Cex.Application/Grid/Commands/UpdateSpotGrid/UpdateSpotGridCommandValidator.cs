using FluentValidation;

namespace Cex.Application.Grid.Commands.UpdateSpotGrid
{
    public class UpdateSpotGridCommandValidator : AbstractValidator<UpdateSpotGridCommand>
    {
        public UpdateSpotGridCommandValidator()
        {
            RuleFor(v => v.UpperPrice)
                .GreaterThan(x => x.LowerPrice).WithMessage("{PropertyName} must be greater than Lower Price.");

            When(v => v.TakeProfit is not null && v.StopLoss is not null, () =>
            {
                RuleFor(v => v.TakeProfit)
                    .GreaterThan(x => x.StopLoss).WithMessage("{PropertyName} must be greater than Stop Loss.");
            });
        }
    }
}