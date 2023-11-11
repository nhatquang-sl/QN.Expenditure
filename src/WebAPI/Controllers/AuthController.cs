﻿using Application.Auth.Commands.ChangePassword;
using Application.Auth.Commands.ConfirmEmail;
using Application.Auth.Commands.Login;
using Application.Auth.Commands.Register;
using Application.Auth.Commands.ResendEmailConfirmation;
using Application.Auth.DTOs;
using MediatR;
using Microsoft.AspNetCore.Authorization;
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

        [HttpPost("login")]
        public async Task<UserAuthDto> Login(LoginCommand loginCommand)
        {
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
    }
}
