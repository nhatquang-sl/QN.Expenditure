using Application.Common.Abstractions;
using Application.Common.Exceptions;
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
                .MustAsync(BeUniqueSymbol);
        }

        public async Task<bool> BeUniqueSymbol(string symbol, CancellationToken cancellationToken)
        {
            if (await _context.SpotOrderSyncSettings.AnyAsync(x => x.Symbol == symbol && x.UserId == _currentUser.Id, cancellationToken))
            {
                return true;
            }

            throw new NotFoundException($"{symbol} not found.");
        }
    }
}
