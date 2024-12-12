using Cex.Application.Common.Abstractions;
using FluentValidation;
using Lib.Application.Abstractions;
using Lib.Application.Exceptions;
using Lib.Application.Extensions;
using Microsoft.EntityFrameworkCore;

namespace Cex.Application.BnbSpotOrder.Commands.UpdateSyncSetting
{
    public class UpdateSyncSettingCommandValidator : AbstractValidator<UpdateSyncSettingCommand>
    {
        private readonly ICexDbContext _context;
        private readonly ICurrentUser _currentUser;

        public UpdateSyncSettingCommandValidator(ICurrentUser currentUser, ICexDbContext context)
        {
            _currentUser = currentUser;
            _context = context;

            RuleFor(x => x.Symbol).NotEmpty()
                .MustAsync(ShouldExists);

            RuleFor(x => x)
                .MustAsync(GreaterThanLastSyncSpotOrder)
                .WithMessage("Last Sync At is greater than last Spot Order sync.");
        }

        public async Task<bool> ShouldExists(string symbol, CancellationToken cancellationToken)
        {
            if (await _context.SpotOrderSyncSettings.AnyAsync(x => x.Symbol == symbol && x.UserId == _currentUser.Id,
                    cancellationToken))
            {
                return true;
            }

            throw new NotFoundException($"{symbol} not found.");
        }

        private async Task<bool> GreaterThanLastSyncSpotOrder(UpdateSyncSettingCommand command,
            CancellationToken cancellationToken)
        {
            var lastSyncSpotOrder = await _context.SpotOrders
                .Where(x => x.Symbol == command.Symbol && x.UserId == _currentUser.Id)
                .OrderByDescending(x => x.UpdateTime)
                .Select(x => x.UpdateTime)
                .FirstOrDefaultAsync(cancellationToken);

            if (lastSyncSpotOrder == default)
            {
                return true;
            }

            return command.LastSyncAt.ToDateTimeFromMilliseconds() > lastSyncSpotOrder;
        }
    }
}