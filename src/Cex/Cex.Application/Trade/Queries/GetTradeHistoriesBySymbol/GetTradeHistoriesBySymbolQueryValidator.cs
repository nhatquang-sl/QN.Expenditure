using FluentValidation;

namespace Cex.Application.Trade.Queries.GetTradeHistoriesBySymbol;

public class GetTradeHistoriesBySymbolQueryValidator : AbstractValidator<GetTradeHistoriesBySymbolQuery>
{
    public GetTradeHistoriesBySymbolQueryValidator()
    {
        RuleFor(x => x.Symbol)
            .NotEmpty()
            .WithMessage("Symbol is required")
            .MaximumLength(20)
            .WithMessage("Symbol cannot exceed 20 characters");

        RuleFor(x => x.PageNumber)
            .GreaterThanOrEqualTo(1)
            .WithMessage("Page number must be at least 1");

        RuleFor(x => x.PageSize)
            .GreaterThanOrEqualTo(1)
            .WithMessage("Page size must be at least 1")
            .LessThanOrEqualTo(100)
            .WithMessage("Page size cannot exceed 100");
    }
}
