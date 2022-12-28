using Microsoft.AspNetCore.Mvc;

namespace playwright_screenshot.Controllers
{
	[ApiController]
	[Route("[controller]")]
	public class HealthController
	{
		[HttpGet]
		public IActionResult Get()
		{
			return new OkResult();
		}
	}
}
