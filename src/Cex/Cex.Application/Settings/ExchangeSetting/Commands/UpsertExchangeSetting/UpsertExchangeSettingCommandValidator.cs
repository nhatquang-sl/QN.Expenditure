using Cex.Domain.Enums;
using FluentValidation;

namespace Cex.Application.Settings.ExchangeSetting.Commands.UpsertExchangeSetting;

public class UpsertExchangeSettingCommandValidator : AbstractValidator<UpsertExchangeSettingCommand>
{
    public UpsertExchangeSettingCommandValidator()
    {
        RuleFor(x => x.ExchangeName)
            .IsInEnum().WithMessage("Invalid exchange name. Supported exchanges: Binance, KuCoin, Coinbase, Kraken, Bybit");

        RuleFor(x => x.ApiKey)
            .NotEmpty().WithMessage("API Key is required")
            .MaximumLength(500).WithMessage("API Key must not exceed 500 characters");

        RuleFor(x => x.Secret)
            .NotEmpty().WithMessage("Secret is required")
            .MaximumLength(500).WithMessage("Secret must not exceed 500 characters");

        RuleFor(x => x.Passphrase)
            .MaximumLength(500).WithMessage("Passphrase must not exceed 500 characters")
            .When(x => !string.IsNullOrEmpty(x.Passphrase));

        // KuCoin requires passphrase
        RuleFor(x => x.Passphrase)
            .NotEmpty().WithMessage("Passphrase is required for KuCoin")
            .When(x => x.ExchangeName == ExchangeName.KuCoin);
    }
}
