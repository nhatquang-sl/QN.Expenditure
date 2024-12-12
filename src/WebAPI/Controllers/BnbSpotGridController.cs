using Cex.Application.BnbSpotGrid.Commands.CreateSpotGrid;
using Cex.Application.BnbSpotGrid.DTOs;
using Cex.Application.BnbSpotGrid.Queries.GetSpotGrids;
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
        [ProducesResponseType(typeof(SpotGridDto), StatusCodes.Status200OK)]
        public Task<SpotGridDto> Create(CreateSpotGridCommand command)
        {
            return _sender.Send(command);
        }

        [HttpGet]
        [ProducesResponseType(typeof(List<SpotGridDto>), StatusCodes.Status200OK)]
        public Task<List<SpotGridDto>> Get()
        {
            return _sender.Send(new GetSpotGridsQuery());
        }
    }
}