using Auth.Application.Account.Commands.ChangeEmail;
using Auth.Application.Account.Commands.ChangePassword;
using Auth.Application.Account.Commands.ConfirmEmail;
using Auth.Application.Account.Commands.ConfirmEmailChange;
using Auth.Application.Account.Commands.ForgotPassword;
using Auth.Application.Account.Commands.Login;
using Auth.Application.Account.Commands.Register;
using Auth.Application.Account.Commands.ResendEmailConfirmation;
using Auth.Application.Account.Commands.RefreshToken;
using Auth.Application.Account.Commands.ResetPassword;
using Auth.Application.Account.DTOs;
using Auth.Application.Account.Queries.GetProfile;
using Auth.Application.Account.Queries.GetUserLoginHistories;
using Auth.Application.Common.Abstractions;
using Auth.Domain.Entities;
using Lib.Application.Extensions;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WebAPI.Middleware;

namespace WebAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthController(ISender sender, IIdentityService identityService, IWebHostEnvironment env)
        : ControllerBase
    {
        private readonly ISender _sender = sender;

        private CookieOptions BuildCookieOptions(DateTime expires)
        {
            return new CookieOptions
            {
                HttpOnly = true,
                Secure = !env.IsDevelopment(),
                SameSite = env.IsDevelopment() ? SameSiteMode.Lax : SameSiteMode.None,
                Expires = expires
            };
        }

        private CookieOptions BuildDeleteCookieOptions()
        {
            return new CookieOptions
            {
                HttpOnly = true,
                Secure = !env.IsDevelopment(),
                SameSite = env.IsDevelopment() ? SameSiteMode.Lax : SameSiteMode.None
            };
        }

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
            var message = await _sender.Send(new ConfirmEmailChangeCommand
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

            // Set httpOnly cookies for tokens
            Response.Cookies.Append("accessToken", result.AccessToken,
                BuildCookieOptions(result.AccessTokenExpires.ToDateTimeFromMilliseconds()));
            Response.Cookies.Append("refreshToken", result.RefreshToken,
                BuildCookieOptions(result.RefreshTokenExpires.ToDateTimeFromMilliseconds()));

            return result;
        }

        [HttpPost("logout")]
        public async Task<IActionResult> Logout()
        {
            // Clear ASP.NET Identity session cookie (.AspNetCore.Identity.Application)
            await identityService.LogoutAsync();

            Response.Cookies.Delete("accessToken", BuildDeleteCookieOptions());
            Response.Cookies.Delete("refreshToken", BuildDeleteCookieOptions());

            return Ok();
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

        [Authorize]
        [HttpGet("login-histories")]
        public Task<List<UserLoginHistory>> GetLoginHistories(int page = 1, int size = 10)
        {
            return _sender.Send(new GetUserLoginHistoriesQuery(page, size));
        }

        [HttpPost("refresh")]
        [ProducesResponseType(typeof(UserAuthDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<UserAuthDto> Refresh()
        {
            var refreshToken = Request.Cookies["refreshToken"]
                ?? throw new BadHttpRequestException("Refresh token cookie is missing.", StatusCodes.Status400BadRequest);

            var command = new RefreshTokenCommand
            {
                RefreshToken = refreshToken,
                IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "",
                UserAgent = Request.Headers.UserAgent.ToString()
            };

            var result = await _sender.Send(command);

            Response.Cookies.Append("accessToken", result.AccessToken,
                BuildCookieOptions(result.AccessTokenExpires.ToDateTimeFromMilliseconds()));
            Response.Cookies.Append("refreshToken", result.RefreshToken,
                BuildCookieOptions(result.RefreshTokenExpires.ToDateTimeFromMilliseconds()));

            return result;
        }

        [Authorize]
        [HttpGet("check")]
        [ProducesResponseType(typeof(UserAuthDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public Task<UserAuthDto> Check()
        {
            return _sender.Send(new GetProfileQuery());
        }
    }
}