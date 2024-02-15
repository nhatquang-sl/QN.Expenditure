using Application.Common.ExServices.Bnb.Models;
using Application.Common.Extensions;
using AutoMapper;
using Domain.Entities;
using System.Reflection;

namespace Application.Common.Mappings
{
    public class MappingProfile : Profile
    {
        public MappingProfile()
        {
            ApplyMappingsFromAssembly(Assembly.GetExecutingAssembly());

            CreateMap<SpotOrderRaw, SpotOrder>()
               .ForMember(x => x.Time, opt => opt.MapFrom(x => x.Time.ToDateTimeFromMilliseconds()))
               .ForMember(x => x.UpdateTime, opt => opt.MapFrom(x => x.UpdateTime.ToDateTimeFromMilliseconds()))
               .ForMember(x => x.WorkingTime, opt => opt.MapFrom(x => x.WorkingTime.ToDateTimeFromMilliseconds()));
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
