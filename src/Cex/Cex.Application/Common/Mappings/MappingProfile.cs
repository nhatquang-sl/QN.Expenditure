using System.Globalization;
using System.Reflection;
using AutoMapper;
using Cex.Application.Grid.Commands.CreateSpotGrid;
using Cex.Domain.Entities;
using Lib.Application.Extensions;
using Lib.ExternalServices.Bnb.Models;
using TradeHistory = Cex.Domain.Entities.TradeHistory;

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
            CreateMap<Lib.ExternalServices.KuCoin.Models.TradeHistory, TradeHistory>()
                .ForMember(x => x.Price, opt => opt.MapFrom(x => decimal.Parse(x.Price, CultureInfo.InvariantCulture)))
                .ForMember(x => x.Size, opt => opt.MapFrom(x => decimal.Parse(x.Size, CultureInfo.InvariantCulture)))
                .ForMember(x => x.Funds, opt => opt.MapFrom(x => decimal.Parse(x.Funds, CultureInfo.InvariantCulture)))
                .ForMember(x => x.Fee, opt => opt.MapFrom(x => decimal.Parse(x.Fee, CultureInfo.InvariantCulture)))
                .ForMember(x => x.FeeRate,
                    opt => opt.MapFrom(x => decimal.Parse(x.FeeRate, CultureInfo.InvariantCulture)))
                .ForMember(x => x.TradedAt, opt => opt.MapFrom(x => x.CreatedAt.ToDateTimeFromMilliseconds()));
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