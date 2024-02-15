using Application.BnbSetting.Commands.UpdateBnbSetting;
using Application.BnbSetting.Queries.GetBnbSettingByUserId;
using Domain.Entities;
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
        public Task<BnbSetting> GetSetting()
            => _sender.Send(new GetBnbSettingByUserIdQuery());

        [HttpPut]
        public Task<BnbSetting> UpdateSetting([FromBody] UpdateBnbSettingCommand request)
            => _sender.Send(request);
    }
}
