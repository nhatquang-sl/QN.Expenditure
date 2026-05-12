using FluentValidation;

namespace Cex.Application.Signals.Queries.GetSignals;

public class GetSignalsQueryValidator : AbstractValidator<GetSignalsQuery>
{
    private static readonly HashSet<string> ValidIntervals =
        ["1min", "5min", "15min", "30min", "1hour", "4hour", "1day"];

    public GetSignalsQueryValidator()
    {
        RuleFor(x => x.From)
            .NotEmpty().WithMessage("From date is required");

        RuleFor(x => x.To)
            .NotEmpty().WithMessage("To date is required")
            .GreaterThanOrEqualTo(x => x.From)
            .WithMessage("To date must be greater than or equal to From date");

        RuleFor(x => x.Interval)
            .Must(i => ValidIntervals.Contains(i!))
            .When(x => !string.IsNullOrEmpty(x.Interval))
            .WithMessage("Interval must be one of: 1min, 5min, 15min, 30min, 1hour, 4hour, 1day");

        RuleFor(x => x.SignalType)
            .IsInEnum()
            .When(x => x.SignalType.HasValue)
            .WithMessage("SignalType must be Long or Short");

        RuleFor(x => x.PageNumber)
            .GreaterThanOrEqualTo(1).WithMessage("PageNumber must be at least 1");

        RuleFor(x => x.PageSize)
            .InclusiveBetween(1, 100).WithMessage("PageSize must be between 1 and 100");
    }
}
