using System.Reflection;
using AutoMapper;
using Cex.Application.Grid.Commands.CreateSpotGrid;
using Cex.Domain.Entities;
using Lib.Application.Extensions;
using Lib.ExternalServices.Bnb.Models;

namespace Cex.Application.Common.Mappings
{
    public class MappingProfile : Profile
    {
        public MappingProfile()
        {
            ApplyMappingsFromAssembly(Assembly.GetExecutingAssembly());

            CreateMap<SpotOrderRaw, SpotOrder>()
                .ForMember(x => x.CreatedAt, opt => opt.MapFrom(x => x.Time.ToDateTimeFromMilliseconds()))
                .ForMember(x => x.UpdatedAt, opt => opt.MapFrom(x => x.UpdateTime.ToDateTimeFromMilliseconds()))
                .ForMember(x => x.WorkingTime, opt => opt.MapFrom(x => x.WorkingTime.ToDateTimeFromMilliseconds()));


            CreateMap<SpotOrder, SpotOrderRaw>()
                .ForMember(x => x.Time, opt => opt.MapFrom(x => x.CreatedAt.ToUnixTimestampMilliseconds()))
                .ForMember(x => x.UpdateTime, opt => opt.MapFrom(x => x.UpdatedAt.ToUnixTimestampMilliseconds()))
                .ForMember(x => x.WorkingTime, opt => opt.MapFrom(x => x.WorkingTime.ToUnixTimestampMilliseconds()));

            CreateMap<CreateSpotGridCommand, SpotGrid>();
        }

        private void ApplyMappingsFromAssembly(Assembly assembly)
        {
            var types = assembly.GetExportedTypes()
                .Where(t => t.GetInterfaces().Any(i =>
                    i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IMapFrom<>)))
                .ToList();

            foreach (var type in types)
            {
                var instance = Activator.CreateInstance(type);

                var methodInfo = type.GetMethod("Mapping")
                                 ?? type.GetInterface("IMapFrom`1")?.GetMethod("Mapping");

                methodInfo?.Invoke(instance, [this]);
            }
        }
    }
}