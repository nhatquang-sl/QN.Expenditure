using AutoMapper;
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
    }

    public class UserAuthDto : UserProfileDto
    {
        public string AccessToken { get; set; }
        public string RefreshToken { get; set; }

        private class Mapping : Profile
        {
            public Mapping()
            {
                CreateMap<UserProfileDto, UserAuthDto>();
            }
        }
    }
}