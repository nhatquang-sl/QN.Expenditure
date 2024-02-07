using Application.Common.Abstractions;
using Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Auth.Queries.GetUserLoginHistories
{
    public record GetUserLoginHistoriesQuery(int Page, int Size) : IRequest<List<UserLoginHistory>>;


    public class GetUserLoginHistoriesQueryHandler : IRequestHandler<GetUserLoginHistoriesQuery, List<UserLoginHistory>>
    {
        private readonly ICurrentUser _currentUser;
        private readonly IApplicationDbContext _dbContext;

        public GetUserLoginHistoriesQueryHandler(ICurrentUser currentUser, IApplicationDbContext dbContext)
        {
            _dbContext = dbContext;
            _currentUser = currentUser;
        }

        public Task<List<UserLoginHistory>> Handle(GetUserLoginHistoriesQuery request, CancellationToken cancellationToken)
            => _dbContext.UserLoginHistories
                    .Where(x => x.UserId == _currentUser.Id)
                    .Skip((request.Page - 1) * request.Size)
                    .Take(request.Size)
                    .ToListAsync(cancellationToken);

    }
}
