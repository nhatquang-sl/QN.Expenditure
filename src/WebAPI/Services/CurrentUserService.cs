﻿using System.Security.Claims;
using Application.Common.Abstractions;
using Infrastructure.Identity;

namespace WebAPI.Services
{
    public class CurrentUserService : ICurrentUserService
    {
        private readonly IHttpContextAccessor _httpContextAccessor;
        public CurrentUserService(IHttpContextAccessor httpContextAccessor)
        {
            _httpContextAccessor = httpContextAccessor;

            if (_httpContextAccessor.HttpContext?.User?.Identity?.IsAuthenticated ?? false)
            {
                var claims = _httpContextAccessor.HttpContext.User;
                Id = claims.FindFirstValue(JwtClaimNames.Id) ?? string.Empty;
                Email = claims.FindFirstValue(JwtClaimNames.Email) ?? string.Empty;
                FirstName = claims.FindFirstValue(JwtClaimNames.FirstName) ?? string.Empty;
                LastName = claims.FindFirstValue(JwtClaimNames.LastName) ?? string.Empty;
                EmailConfirmed = bool.Parse(claims.FindFirstValue(JwtClaimNames.EmailConfirmed) ?? false.ToString());
            }
        }

        public string Id { get; set; }
        public string Email { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public bool EmailConfirmed { get; set; }
    }
}
