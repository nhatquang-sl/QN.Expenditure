using Cex.Application.ExchangeConfigs.Commands.DeleteExchangeConfig;
using Cex.Application.ExchangeConfigs.Commands.UpsertExchangeConfig;
using Cex.Application.ExchangeConfigs.DTOs;
using Cex.Application.ExchangeConfigs.Queries.GetExchangeConfigs;
using Cex.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace WebAPI.Controllers
{
    [Route("api/exchange-configs")]
    [ApiController]
    public class ExchangeConfigsController(ISender sender) : ControllerBase
    {
        [HttpGet]
        public Task<List<ExchangeConfigDto>> GetConfigs()
        {
            return sender.Send(new GetExchangeConfigsQuery());
        }


        [HttpPost]
        public Task<ExchangeConfigDto> UpsertConfig([FromBody] UpsertExchangeConfigCommand request)
        {
            return sender.Send(request);
        }

        [HttpDelete("{exchangeName}")]
        public async Task<IActionResult> DeleteConfig(ExchangeName exchangeName)
        {
            await sender.Send(new DeleteExchangeConfigCommand(exchangeName));
            return NoContent();
        }
    }
}