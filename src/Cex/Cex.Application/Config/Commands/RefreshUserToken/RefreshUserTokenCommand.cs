using Cex.Application.Common.Abstractions;
using Cex.Application.Common.Configs;
using Cex.Application.Config.Dtos;
using Lib.Application.Exceptions;
using Lib.Application.Logging;
using Lib.ExternalServices.Cex;
using Lib.ExternalServices.Cex.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Text.Json;

namespace Cex.Application.Config.Commands.RefreshUserToken
{
    public record RefreshUserTokenCommand(string AccessToken, string RefreshToken) : IRequest<(string, string)> { }

    public class RefreshUserTokenCommandHandler(ICexDbContext cexDbContext, IOptions<CexConfig> cexConfig, ICexService cexService
        , ILogTrace logTrace)
    : IRequestHandler<RefreshUserTokenCommand, (string, string)>
    {
        private readonly ICexDbContext _cexDbContext = cexDbContext;
        private readonly CexConfig _cexConfig = cexConfig.Value;
        private readonly ICexService _cexService = cexService;
        private readonly ILogTrace _logTrace = logTrace;

        public async Task<(string, string)> Handle(RefreshUserTokenCommand command, CancellationToken cancellationToken)
        {
            var cexUser = CexUtils.DecodeToken(command.AccessToken);
            var liveTime = cexUser.ExpiredAt - DateTimeOffset.UtcNow;
            _logTrace.LogInformation($"Expire in next {liveTime.TotalMinutes} minutes");
            if (liveTime.TotalMinutes > 3)
                return (command.AccessToken, command.RefreshToken);

            var res = await _cexService.RefreshToken(new RefreshTokenRequest(_cexConfig.ClientId, command.RefreshToken));
            if (!res.Ok) return ("", "");

            var token = new UserToken
            {
                AccessToken = res.Data.AccessToken,
                RefreshToken = res.Data.RefreshToken
            };
            var config = await _cexDbContext.Configs
                .Where(x => x.Key == "USER_TOKEN")
                .FirstOrDefaultAsync(cancellationToken) ?? throw new NotFoundException("USER_TOKEN");

            config.Value = JsonSerializer.Serialize(token);
            _cexDbContext.Configs.Update(config);
            await _cexDbContext.SaveChangesAsync(cancellationToken);

            return (token.AccessToken, token.RefreshToken);
        }
    }
}
