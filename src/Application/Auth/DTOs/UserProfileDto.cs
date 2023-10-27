using Application.Common.Mappings;

namespace Application.Auth.DTOs
{
    public class UserProfileDto
    {
        public string Id { get; set; }
        public string Email { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
    }

    public class UserAuthDto : UserProfileDto, IMapFrom<UserProfileDto>
    {
        public string AccessToken { get; set; }
        public string RefreshToken { get; set; }
    }
}