using Application.BnbSpotGrid.Commands.CreateSpotGrid;
using Application.BnbSpotGrid.DTOs;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace WebAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class BnbSpotGridController(ISender sender) : ControllerBase
    {
        private readonly ISender _sender = sender;

        [HttpPost]
        [ProducesResponseType(typeof(CreateSpotGridBadRequest), StatusCodes.Status400BadRequest)]
        //[ProducesResponseType(typeof(SpotGridDto), StatusCodes.Status200OK)]
        public Task<SpotGridDto> Create(CreateSpotGridCommand command)
            => _sender.Send(command);
    }
}
