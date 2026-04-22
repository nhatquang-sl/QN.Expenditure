using Auth.Application.Account.DTOs;
using AutoMapper;

namespace Auth.Application.Common.Mappings;

public class MappingProfile : Profile
{
    public MappingProfile()
    {
        CreateMap<UserProfileDto, UserAuthDto>();
    }
}
