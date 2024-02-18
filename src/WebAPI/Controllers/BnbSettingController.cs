using Application.BnbSetting.Commands.UpdateBnbSetting;
using Application.BnbSetting.DTOs;
using Application.BnbSetting.Queries.GetBnbSettingByUserId;
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
            => _sender.Send(new GetBnbSettingByUserIdQuery());

        [HttpPut]
        public Task<BnbSettingDto> UpdateSetting([FromBody] UpdateBnbSettingCommand request)
            => _sender.Send(request);
    }
}
