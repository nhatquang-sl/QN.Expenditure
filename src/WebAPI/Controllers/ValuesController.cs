using Lib.Application.Configs;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace WebAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ValuesController : ControllerBase
    {
        private readonly ApplicationConfig _applicationConfig;

        public ValuesController(IOptions<ApplicationConfig> applicationConfig)
        {
            _applicationConfig = applicationConfig.Value;
        }

        [HttpGet]
        public object Get()
        {
            return _applicationConfig;
        }
    }
}