using Application.Common.Abstractions;
using Application.Common.Exceptions;
using Application.Common.Extensions;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Application.BnbSpotOrder.Commands.UpdateSyncSetting
{
    public class UpdateSyncSettingCommandValidator : AbstractValidator<UpdateSyncSettingCommand>
    {
        private readonly ICurrentUser _currentUser;
        private readonly IApplicationDbContext _context;
        public UpdateSyncSettingCommandValidator(ICurrentUser currentUser, IApplicationDbContext context)
        {
            _currentUser = currentUser;
            _context = context;

            RuleFor(x => x.Symbol).NotEmpty()
                .MustAsync(ShouldExists);

            RuleFor(x => x)
                .MustAsync(GreaterThanLastSyncSpotOrder).WithMessage("Last Sync At is greater than last Spot Order sync.");
        }

        public async Task<bool> ShouldExists(string symbol, CancellationToken cancellationToken)
        {
            if (await _context.SpotOrderSyncSettings.AnyAsync(x => x.Symbol == symbol && x.UserId == _currentUser.Id, cancellationToken))
            {
                return true;
            }

            throw new NotFoundException($"{symbol} not found.");
        }

        private async Task<bool> GreaterThanLastSyncSpotOrder(UpdateSyncSettingCommand command, CancellationToken cancellationToken)
        {
            var lastSyncSpotOrder = await _context.SpotOrders
                .Where(x => x.Symbol == command.Symbol && x.UserId == _currentUser.Id)
                .OrderByDescending(x => x.UpdateTime)
                .Select(x => x.UpdateTime)
                .FirstOrDefaultAsync(cancellationToken);

            if (lastSyncSpotOrder == default) return true;

            return command.LastSyncAt.ToDateTimeFromMilliseconds() > lastSyncSpotOrder;
        }
    }
}
