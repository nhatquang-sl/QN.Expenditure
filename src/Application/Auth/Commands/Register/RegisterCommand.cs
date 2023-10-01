using Application.Common.Abstractions;
using MediatR;

namespace Application.Auth.Commands.Register
{
    public class RegisterCommand : IRequest<RegisterResult>
    {
        public string Email { get; set; }
        public string Password { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
    }

    public class RegisterResult
    {
        public RegisterResult(string userId)
        {
            UserId = userId;
        }

        public string UserId { get; set; }
    }

    public class RegisterCommandHandler : IRequestHandler<RegisterCommand, RegisterResult>
    {
        private readonly IIdentityService _context;

        public RegisterCommandHandler(IIdentityService context)
        {
            _context = context;
        }

        public async Task<RegisterResult> Handle(RegisterCommand request, CancellationToken cancellationToken)
        {
            var userId = await _context.CreateUserAsync(request);

            return new RegisterResult(userId);
        }
    }
}
