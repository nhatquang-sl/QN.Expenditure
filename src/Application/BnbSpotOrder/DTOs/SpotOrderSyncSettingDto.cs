﻿using Application.Common.Extensions;
using Application.Common.Mappings;
using AutoMapper;
using Domain.Entities;

namespace Application.BnbSpotOrder.DTOs
{
    public class SpotOrderSyncSettingDto : IMapFrom<SpotOrderSyncSetting>
    {
        public string Symbol { get; set; }
        public long LastSyncAt { get; set; }

        public void Mapping(Profile profile)
        {
            profile.CreateMap<SpotOrderSyncSetting, SpotOrderSyncSettingDto>()
                .ForMember(x => x.LastSyncAt, opt => opt.MapFrom(x => x.LastSyncAt.ToUnixTimestampMilliseconds()));
        }
    }

    public class SpotOrderSyncSettingUpdateDto
    {
        public long LastSyncAt { get; set; }
    }
}
