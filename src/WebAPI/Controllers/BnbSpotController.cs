﻿using Application.BnbSpotOrder.Commands.CreateSyncSetting;
using Application.BnbSpotOrder.Commands.DeleteSyncSetting;
using Application.BnbSpotOrder.Commands.UpdateSyncSetting;
using Application.BnbSpotOrder.DTOs;
using Application.BnbSpotOrder.Queries.GetSpotOrders;
using Application.BnbSpotOrder.Queries.GetSyncSettings;
using Application.Common.ExServices.Bnb.Models;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WebAPI.Middleware;

namespace WebAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class BnbSpotController(ISender sender) : ControllerBase
    {
        private readonly ISender _sender = sender;

        [HttpGet("sync-settings")]
        public Task<List<SpotOrderSyncSettingDto>> GetSyncSettings()
            => _sender.Send(new GetSyncSettingsQuery());

        [HttpPost("sync-settings")]
        [ProducesResponseType(typeof(Conflict), StatusCodes.Status409Conflict)]
        [ProducesResponseType(typeof(SpotOrderSyncSettingDto), StatusCodes.Status200OK)]
        public Task<SpotOrderSyncSettingDto> CreateSyncSetting([FromBody] CreateSyncSettingCommand request)
            => _sender.Send(request);

        [HttpPut("sync-settings/{symbol}")]
        public Task<SpotOrderSyncSettingDto> UpdateSyncSetting(string symbol, [FromBody] SpotOrderSyncSettingUpdateDto request)
            => _sender.Send(new UpdateSyncSettingCommand(symbol, request.LastSyncAt));

        [HttpDelete("sync-settings/{symbol}")]
        public Task<SpotOrderSyncSettingDto> DeleteSyncSetting(string symbol)
            => _sender.Send(new DeleteSyncSettingCommand(symbol));

        [HttpGet]
        public Task<List<SpotOrderRaw>> GetSpotOrders()
            => _sender.Send(new GetSpotOrdersQuery());
    }
}
