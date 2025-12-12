using Auth.Application.Common.Abstractions;
using Auth.Domain.Entities;
using Lib.Application.Abstractions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Auth.Application.Account.Queries.GetUserLoginHistories
{
    public record GetUserLoginHistoriesQuery(int Page, int Size) : IRequest<List<UserLoginHistory>>;

    public class GetUserLoginHistoriesQueryHandler(ICurrentUser currentUser, IAuthDbContext dbContext)
        : IRequestHandler<GetUserLoginHistoriesQuery, List<UserLoginHistory>>
    {
        public Task<List<UserLoginHistory>> Handle(GetUserLoginHistoriesQuery request,
            CancellationToken cancellationToken)
        {
            return dbContext.UserLoginHistories
                .Where(x => x.UserId == currentUser.Id)
                .OrderByDescending(x => x.Id)
                .Skip((request.Page - 1) * request.Size)
                .Take(request.Size)
                .ToListAsync(cancellationToken);
        }
    }
}