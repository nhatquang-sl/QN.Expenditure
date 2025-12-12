using Cex.Application.Settings.ExchangeSetting.Commands.DeleteExchangeSetting;
using Cex.Application.Settings.ExchangeSetting.Commands.UpsertExchangeSetting;
using Cex.Application.Settings.ExchangeSetting.DTOs;
using Cex.Application.Settings.ExchangeSetting.Queries.GetExchangeSettings;
using Cex.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace WebAPI.Controllers;

[Authorize]
[Route("api/exchange-settings")]
[ApiController]
public class ExchangeSettingsController(ISender sender) : ControllerBase
{
    [HttpGet]
    public Task<List<ExchangeSettingDto>> GetSettings()
        => sender.Send(new GetExchangeSettingsQuery());

    [HttpPost]
    public Task<ExchangeSettingDto> UpsertSetting([FromBody] UpsertExchangeSettingCommand request)
        => sender.Send(request);

    [HttpDelete("{exchangeName}")]
    public async Task<IActionResult> DeleteSetting(ExchangeName exchangeName)
    {
        await sender.Send(new DeleteExchangeSettingCommand(exchangeName));
        return NoContent();
    }
}
