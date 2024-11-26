using Cex.Application.Common.Abstractions;
using Cex.Application.Config.Dtos;
using Lib.Application.Exceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace Cex.Application.Config.Queries.GetUserToken
{
    public class GetUserTokenQuery : IRequest<(string, string)> { }

    public class GetUserTokenQueryHandler(ICexDbContext cexDbContext)
        : IRequestHandler<GetUserTokenQuery, (string, string)>
    {
        private readonly ICexDbContext _cexDbContext = cexDbContext;

        public async Task<(string, string)> Handle(GetUserTokenQuery request, CancellationToken cancellationToken)
        {
            var config = await _cexDbContext.Configs
                .Where(x => x.Key == "USER_TOKEN")
                .FirstOrDefaultAsync(cancellationToken) ?? throw new NotFoundException("USER_TOKEN");

            var token = JsonSerializer.Deserialize<UserToken>(config.Value);

            return (token?.AccessToken ?? "", token?.RefreshToken ?? "");
        }
    }
}
