using FluentValidation;

namespace Cex.Application.Sync.SyncSetting.Commands.UpsertSyncSetting;

public class UpsertSyncSettingCommandValidator : AbstractValidator<UpsertSyncSettingCommand>
{
    public UpsertSyncSettingCommandValidator()
    {
        RuleFor(x => x.Symbol)
            .NotEmpty()
            .WithMessage("Symbol is required")
            .MaximumLength(50)
            .WithMessage("Symbol must not exceed 50 characters");

        RuleFor(x => x.StartSync)
            .GreaterThan(0)
            .WithMessage("Start Sync must be a valid timestamp");
    }
}
