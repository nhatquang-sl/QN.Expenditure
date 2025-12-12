using Cex.Application.Common.Abstractions;
using FluentValidation;
using Lib.Application.Abstractions;
using Lib.Application.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace Cex.Application.BnbSpotOrder.Commands.CreateSyncSetting
{
    public class CreateSyncSettingCommandValidator : AbstractValidator<CreateSyncSettingCommand>
    {
        private readonly ICexDbContext _context;
        private readonly ICurrentUser _currentUser;

        public CreateSyncSettingCommandValidator(ICurrentUser currentUser, ICexDbContext context)
        {
            _currentUser = currentUser;
            _context = context;

            RuleFor(x => x.Symbol).NotEmpty()
                .MustAsync(BeUniqueSymbol);
        }

        public async Task<bool> BeUniqueSymbol(string symbol, CancellationToken cancellationToken)
        {
            if (await _context.SpotOrderSyncSettings.AnyAsync(x => x.Symbol == symbol && x.UserId == _currentUser.Id,
                    cancellationToken))
            {
                throw new ConflictException($"{symbol} already exists.");
            }

            return true;
        }
    }
}