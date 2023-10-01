using Application.Auth.Commands.Register;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace WebAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly ISender _sender;
        public AuthController(ISender sender)
        {
            _sender = sender;
        }

        [Route("register")]
        public async Task<RegisterResult> Register(RegisterCommand registerCommand)
        {
            var result = await _sender.Send(registerCommand);

            return result;
        }
    }
}
