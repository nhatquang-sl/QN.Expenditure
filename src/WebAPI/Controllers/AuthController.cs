using Application.Auth.Commands.ChangeEmail;
using Application.Auth.Commands.ChangePassword;
using Application.Auth.Commands.ConfirmEmail;
using Application.Auth.Commands.ConfirmEmailChange;
using Application.Auth.Commands.ForgotPassword;
using Application.Auth.Commands.Login;
using Application.Auth.Commands.Register;
using Application.Auth.Commands.ResendEmailConfirmation;
using Application.Auth.Commands.ResetPassword;
using Application.Auth.DTOs;
using Application.Auth.Queries.GetUserLoginHistories;
using Domain.Entities;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WebAPI.Middleware;

namespace WebAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthController(ISender sender) : ControllerBase
    {
        private readonly ISender _sender = sender;

        [HttpPost("register")]
        [ProducesResponseType(typeof(Conflict), StatusCodes.Status409Conflict)]
        [ProducesResponseType(typeof(RegisterResult), StatusCodes.Status200OK)]
        public async Task<RegisterResult> Register(RegisterCommand registerCommand)
        {
            var IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "";
            var UserAgent = Request.Headers.UserAgent.ToString();
            var result = await _sender.Send(registerCommand);

            return result;
        }

        [HttpGet("confirm-email")]
        public async Task<IActionResult> ConfirmEmail(string userId, string code)
        {
            var succeeded = await _sender.Send(new ConfirmEmailCommand(userId, code));
            return Ok(new { succeeded });
        }

        [HttpGet("confirm-email-change")]
        public async Task<IActionResult> ConfirmEmailChange(string userId, string code, string email)
        {
            var message = await _sender.Send(new ConfirmEmailChangeCommand()
            {
                UserId = userId,
                Code = code,
                Email = email
            });
            return Ok(new { message });
        }

        [HttpPost("login")]
        [ProducesResponseType(typeof(BadRequest), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(UserAuthDto), StatusCodes.Status200OK)]
        public async Task<UserAuthDto> Login(LoginCommand loginCommand)
        {
            loginCommand.IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "";
            loginCommand.UserAgent = Request.Headers.UserAgent.ToString();
            var result = await _sender.Send(loginCommand);

            return result;
        }

        [Authorize]
        [HttpPost("resend-email-confirmation")]
        public async Task<IActionResult> ResendEmailConfirmation()
        {
            await _sender.Send(new ResendEmailConfirmationCommand());

            return Ok();
        }

        [Authorize]
        [HttpPost("change-password")]
        public async Task<IActionResult> ChangePassword(ChangePasswordCommand command)
        {
            await _sender.Send(command);

            return Ok();
        }

        [Authorize]
        [HttpPost("change-email")]
        public async Task<IActionResult> ChangeEmail(ChangeEmailCommand command)
        {
            var message = await _sender.Send(command);

            return Ok(new { message });
        }

        [HttpPost("forgot-password")]
        public async Task<IActionResult> ForgotPassword(ForgotPasswordCommand command)
        {
            await _sender.Send(command);

            return Ok();
        }

        [HttpPost("reset-password")]
        public async Task<IActionResult> ResetPassword(ResetPasswordCommand command)
        {
            await _sender.Send(command);

            return Ok();
        }

        [HttpGet("login-histories")]
        public Task<List<UserLoginHistory>> GetLoginHistories(int page = 1, int size = 10)
            => _sender.Send(new GetUserLoginHistoriesQuery(page, size));

    }
}
