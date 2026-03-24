using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Auth.Infrastructure.Identity
{
    internal class JwtBearerSetup : IConfigureNamedOptions<JwtBearerOptions>
    {
        private readonly JwtConfig _jwtConfig;

        public JwtBearerSetup(IOptions<JwtConfig> jwtConfig)
        {
            _jwtConfig = jwtConfig.Value;
        }

        public void Configure(string? name, JwtBearerOptions options)
        {
            Configure(options);
        }

        public void Configure(JwtBearerOptions options)
        {
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer = _jwtConfig.Issuer,
                ValidAudience = _jwtConfig.Audience,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtConfig.AccessTokenSecretKey))
            };

            options.Events = new JwtBearerEvents
            {
                OnMessageReceived = context =>
                {
                    // 1. Cookie takes priority (browser clients)
                    var cookieToken = context.Request.Cookies["accessToken"];
                    if (string.IsNullOrWhiteSpace(cookieToken))
                    {
                        return Task.CompletedTask;
                    }

                    context.Token = cookieToken;
                    return Task.CompletedTask;

                    // // 2. Bearer header fallback (Swagger / API clients)
                    // var authorization = context.Request.Headers[HeaderNames.Authorization].ToString();
                    // if (authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
                    // {
                    //     context.Token = authorization["Bearer ".Length..].Trim();
                    // }
                }
            };
        }
    }
}