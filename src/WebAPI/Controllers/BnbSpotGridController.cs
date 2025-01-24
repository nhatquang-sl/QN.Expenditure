using Cex.Application.Grid.Commands.CreateSpotGrid;
using Cex.Application.Grid.Commands.DeleteSpotGrid;
using Cex.Application.Grid.DTOs;
using Cex.Application.Grid.Queries.GetSpotGrids;
using Lib.Application.Exceptions;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using WebAPI.Middleware;

namespace WebAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [ProducesResponseType(typeof(BadRequest), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(UnprocessableEntity[]), StatusCodes.Status422UnprocessableEntity)]
    public class BnbSpotGridController(ISender sender) : ControllerBase
    {
        [HttpPost]
        [ProducesResponseType(typeof(SpotGridDto), StatusCodes.Status200OK)]
        public Task<SpotGridDto> Create(CreateSpotGridCommand command)
        {
            return sender.Send(command);
        }

        [HttpGet]
        [ProducesResponseType(typeof(List<SpotGridDto>), StatusCodes.Status200OK)]
        public Task<List<SpotGridDto>> Get()
        {
            return sender.Send(new GetSpotGridsQuery());
        }

        [HttpDelete("{spotGridId:long}")]
        [ProducesResponseType(typeof(SpotGridDto), StatusCodes.Status200OK)]
        public Task<SpotGridDto> Delete(long spotGridId)
        {
            return sender.Send(new DeleteSpotGridCommand(spotGridId));
        }
    }
}