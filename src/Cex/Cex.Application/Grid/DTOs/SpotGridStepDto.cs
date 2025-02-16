using AutoMapper;
using Cex.Application.Common.Mappings;
using Cex.Domain.Entities;
using Lib.Application.Extensions;

namespace Cex.Application.Grid.DTOs
{
    public class SpotGridStepDto : IMapFrom<SpotGridStep>
    {
        public long Id { get; set; }
        public decimal BuyPrice { get; set; }
        public decimal SellPrice { get; set; }
        public decimal Qty { get; set; }
        public string? OrderId { get; set; }
        public string Status { get; set; }

        public void Mapping(Profile profile)
        {
            profile.CreateMap<SpotGridStep, SpotGridStepDto>()
                .ForMember(x => x.Status, opt => opt.MapFrom(x => x.Status.GetDescription()));
        }
    }
}