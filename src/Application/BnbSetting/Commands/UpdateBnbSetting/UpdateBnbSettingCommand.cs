﻿using Application.Common.Abstractions;
using AutoMapper;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.BnbSetting.Commands.UpdateBnbSetting
{
    public record UpdateBnbSettingCommand(string ApiKey, string SecretKey) : IRequest<Domain.Entities.BnbSetting>;

    public class UpdateBnbSettingCommandHandler(ICurrentUser currentUser, IApplicationDbContext applicationDbContext)
        : IRequestHandler<UpdateBnbSettingCommand, Domain.Entities.BnbSetting>
    {
        private readonly ICurrentUser _currentUser = currentUser;
        private readonly IApplicationDbContext _applicationDbContext = applicationDbContext;

        public async Task<Domain.Entities.BnbSetting> Handle(UpdateBnbSettingCommand request, CancellationToken cancellationToken)
        {
            var entity = await _applicationDbContext.BnbSettings.FirstOrDefaultAsync(x => x.UserId == _currentUser.Id, cancellationToken);
            if (entity == null)
            {
                entity = new Domain.Entities.BnbSetting
                {
                    UserId = _currentUser.Id,
                    ApiKey = request.ApiKey,
                    SecretKey = request.SecretKey
                };
                _applicationDbContext.BnbSettings.Add(entity);
            }
            else
            {
                entity.ApiKey = request.ApiKey;
                entity.SecretKey = request.SecretKey;
                _applicationDbContext.BnbSettings.Update(entity);
            }

            await _applicationDbContext.SaveChangesAsync(cancellationToken);

            return entity ?? default;
        }
    }
}
