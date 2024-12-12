using Cex.Application.BnbSpotOrder.Commands.CreateSyncSetting;
using Cex.Application.BnbSpotOrder.Commands.DeleteSyncSetting;
using Cex.Application.BnbSpotOrder.Commands.UpdateSyncSetting;
using Cex.Application.BnbSpotOrder.DTOs;
using Cex.Application.BnbSpotOrder.Queries.GetSpotOrders;
using Cex.Application.BnbSpotOrder.Queries.GetSpotOrdersBySymbol;
using Cex.Application.BnbSpotOrder.Queries.GetSyncSettings;
using Lib.ExternalServices.Bnb.Models;
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
        {
            return _sender.Send(new GetSyncSettingsQuery());
        }

        [HttpPost("sync-settings")]
        [ProducesResponseType(typeof(Conflict), StatusCodes.Status409Conflict)]
        [ProducesResponseType(typeof(SpotOrderSyncSettingDto), StatusCodes.Status200OK)]
        public Task<SpotOrderSyncSettingDto> CreateSyncSetting([FromBody] CreateSyncSettingCommand request)
        {
            return _sender.Send(request);
        }

        [HttpPut("sync-settings/{symbol}")]
        [ProducesResponseType(typeof(NotFound), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(BadRequest), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(SpotOrderSyncSettingDto), StatusCodes.Status200OK)]
        public Task<SpotOrderSyncSettingDto> UpdateSyncSetting(string symbol,
            [FromBody] SpotOrderSyncSettingUpdateDto request)
        {
            return _sender.Send(new UpdateSyncSettingCommand(symbol, request.LastSyncAt));
        }

        [HttpDelete("sync-settings/{symbol}")]
        public Task<SpotOrderSyncSettingDto> DeleteSyncSetting(string symbol)
        {
            return _sender.Send(new DeleteSyncSettingCommand(symbol));
        }

        [HttpPost("sync-settings/{symbol}/sync")]
        public Task<SpotOrderSyncSettingDto> TriggerSync(string symbol)
        {
            return _sender.Send(new DeleteSyncSettingCommand(symbol));
        }

        [HttpGet]
        public Task<List<SpotOrderRaw>> GetSpotOrders()
        {
            return _sender.Send(new GetSpotOrdersQuery());
        }

        [HttpGet("/{symbol}")]
        public Task<List<SpotOrderRaw>> GetSpotOrdersBySymbol(string symbol)
        {
            return _sender.Send(new GetSpotOrdersBySymbolQuery(symbol));
        }
    }
}