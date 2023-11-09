using Application.Auth.Commands.Register;
using Application.Auth.DTOs;
using Application.Common.Abstractions;
using AutoMapper;
using Infrastructure.Identity;

namespace Infrastructure
{
    internal class MappingProfile : Profile
    {
        public MappingProfile()
        {
            CreateMap<RegisterCommand, ApplicationUser>()
                .ForMember(x => x.UserName, opt => opt.MapFrom(x => x.Email));
            CreateMap<ApplicationUser, UserProfileDto>();

            CreateMap<ICurrentUserService, UserProfileDto>().ReverseMap();
        }
    }
}
