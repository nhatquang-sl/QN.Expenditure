using Cex.Application.Sync.SyncSetting.Commands.DeleteSyncSetting;
using Cex.Application.Sync.SyncSetting.Commands.UpsertSyncSetting;
using Cex.Application.Sync.SyncSetting.DTOs;
using Cex.Application.Sync.SyncSetting.Queries.GetSyncSettings;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace WebAPI.Controllers;

[Authorize]
[Route("api/sync-settings")]
[ApiController]
public class SyncSettingsController(ISender sender) : ControllerBase
{
    [HttpGet]
    public Task<List<SyncSettingDto>> GetSettings()
        => sender.Send(new GetSyncSettingsQuery());

    [HttpPost]
    public Task<SyncSettingDto> UpsertSetting([FromBody] UpsertSyncSettingCommand request)
        => sender.Send(request);

    [HttpDelete("{symbol}")]
    public async Task<IActionResult> DeleteSetting(string symbol)
    {
        await sender.Send(new DeleteSyncSettingCommand(symbol));
        return NoContent();
    }
}
