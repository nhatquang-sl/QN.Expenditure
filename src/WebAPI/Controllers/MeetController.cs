using Microsoft.AspNetCore.Mvc;

namespace WebAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class MeetController : ControllerBase
    {
        // GET
        public IActionResult Index()
        {
            return Redirect("https://meet.google.com/nwi-uhcz-xfw");
        }
    }
}