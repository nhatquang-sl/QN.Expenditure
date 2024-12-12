using Cex.Application.BnbSetting.Commands.UpdateBnbSetting;
using Cex.Application.BnbSetting.DTOs;
using Cex.Application.BnbSetting.Queries.GetBnbSettingByUserId;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace WebAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class BnbSettingController(ISender sender) : ControllerBase
    {
        private readonly ISender _sender = sender;

        [HttpGet]
        public Task<BnbSettingDto> GetSetting()
        {
            return _sender.Send(new GetBnbSettingByUserIdQuery());
        }

        [HttpPut]
        public Task<BnbSettingDto> UpdateSetting([FromBody] UpdateBnbSettingCommand request)
        {
            return _sender.Send(request);
        }
    }
}