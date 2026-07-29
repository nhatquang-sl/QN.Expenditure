using System.Text.Json.Serialization;
using Lib.Application.Abstractions;

namespace Auth.Application.Account.DTOs
{
    public class UserProfileDto : ICurrentUser
    {
        public string Id { get; set; }
        public string Email { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public bool EmailConfirmed { get; set; }
        public long AccessTokenExpires { get; set; }
        public long RefreshTokenExpires { get; set; }

        public static UserProfileDto From(ICurrentUser user) => new()
        {
            Id = user.Id,
            Email = user.Email,
            FirstName = user.FirstName,
            LastName = user.LastName,
            EmailConfirmed = user.EmailConfirmed,
            AccessTokenExpires = user.AccessTokenExpires,
            RefreshTokenExpires = user.RefreshTokenExpires
        };
    }

    public class UserAuthDto : UserProfileDto
    {
        [JsonIgnore] public string AccessToken { get; set; }

        [JsonIgnore] public string RefreshToken { get; set; }
    }
}
