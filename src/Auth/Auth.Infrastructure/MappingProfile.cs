using Auth.Application.Account.Commands.Register;
using Auth.Application.Account.DTOs;
using Auth.Infrastructure.Identity;
using AutoMapper;

namespace Auth.Infrastructure
{
    internal class MappingProfile : Profile
    {
        public MappingProfile()
        {
            CreateMap<RegisterCommand, ApplicationUser>()
                .ForMember(x => x.UserName, opt => opt.MapFrom(x => x.Email));
            CreateMap<ApplicationUser, UserProfileDto>();
        }
    }
}