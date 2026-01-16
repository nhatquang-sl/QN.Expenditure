using FluentValidation;

namespace Cex.Application.Trade.Queries.GetTradeStatisticsBySymbol;

public class GetTradeStatisticsBySymbolQueryValidator
    : AbstractValidator<GetTradeStatisticsBySymbolQuery>
{
    public GetTradeStatisticsBySymbolQueryValidator()
    {
        RuleFor(x => x.Symbol)
            .NotEmpty()
            .WithMessage("Symbol is required")
            .MaximumLength(20)
            .WithMessage("Symbol cannot exceed 20 characters")
            .Matches(@"^[A-Z0-9]+-[A-Z0-9]+$")
            .WithMessage("Symbol must be in format: BASE-QUOTE (e.g., BTC-USDT)");
    }
}
