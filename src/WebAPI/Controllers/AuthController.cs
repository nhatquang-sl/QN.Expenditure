using Application.Auth.Commands.ConfirmEmail;
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

        [HttpPost("register")]
        public async Task<RegisterResult> Register(RegisterCommand registerCommand)
        {
            var result = await _sender.Send(registerCommand);

            return result;
        }

        [HttpGet("confirm-email")]
        public async Task<IActionResult> ConfirmEmail(string userId, string code)
        {
            var succeeded = await _sender.Send(new ConfirmEmailCommand(userId, code));
            return Ok(new { succeeded });
        }
    }
}
