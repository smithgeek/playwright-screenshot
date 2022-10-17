using Microsoft.AspNetCore.Mvc;
using Microsoft.Playwright;

namespace playwright_screenshot.Controllers
{
	[ApiController]
	[Route("[controller]")]
	public class ScreenshotController : ControllerBase
	{
		private readonly IBrowser browser;
		private readonly IHttpClientFactory httpClientFactory;
		private readonly ILogger<ScreenshotController> logger;

		public ScreenshotController(ILogger<ScreenshotController> logger, IBrowser browser,
			IHttpClientFactory httpClientFactory)
		{
			this.logger = logger;
			this.browser = browser;
			this.httpClientFactory = httpClientFactory;
		}

		[HttpGet]
		public async Task<IActionResult> Get(
			[FromQuery] string url,
			[FromQuery(Name = "h")] int height = 720,
			[FromQuery(Name = "w")] int width = 1280,
			[FromQuery(Name = "fp")] bool fullPage = false,
			[FromQuery(Name = "l")] string? locator = null,
			[FromQuery(Name = "f")] string? format = null,
			[FromQuery(Name = "q")] int quality = 100)
		{
			try
			{
				var screenshot = await TakeScreenshot(new ScreenshotOptions
				{
					Format = format,
					FullPage = fullPage,
					Height = height,
					Locator = locator,
					Quality = quality,
					Url = url,
					Width = width
				});
				if (screenshot != null)
				{
					return File(screenshot.Bytes, screenshot.ContentType);
				}
			}
			catch (Exception e)
			{
				logger.LogError(e, "Error generating screenshot");
			}
			throw new Exception("Unable to generate screenshot");
		}

		[HttpPost]
		public async Task<IActionResult> Post([FromBody] ScreenshotOptions options, [FromQuery] Uri presignedUrl)
		{
			var screenshot = await TakeScreenshot(options);
			if (screenshot != null)
			{
				using var stream = new MemoryStream(screenshot.Bytes);
				if (await Upload(stream, presignedUrl))
				{
					return Ok();
				}
			}
			return Problem("Unable to upload image.");
		}

		private async Task<Screenshot?> TakeScreenshot(ScreenshotOptions options)
		{
			var page = await browser.NewPageAsync();
			await page.SetViewportSizeAsync(options.Width, options.Height);
			var response = await page.GotoAsync(options.Url);
			if (response != null)
			{
				await response.FinishedAsync();
				var screenshotType = options.Format == "png" ? ScreenshotType.Png : ScreenshotType.Jpeg;
				var contentType = options.Format == "png" ? "image/png" : "image/jpeg";
				byte[] bytes;
				if (!string.IsNullOrWhiteSpace(options.Locator))
				{
					bytes = await page.Locator(options.Locator).ScreenshotAsync(new LocatorScreenshotOptions
					{
						Type = screenshotType,
						Quality = screenshotType == ScreenshotType.Jpeg ? options.Quality : null
					});
				}
				else
				{
					bytes = await page.ScreenshotAsync(new PageScreenshotOptions
					{
						FullPage = options.FullPage,
						Type = screenshotType,
						Quality = screenshotType == ScreenshotType.Jpeg ? options.Quality : null
					});
				}
				return new Screenshot
				{
					Bytes = bytes,
					ContentType = contentType
				};
			}
			return null;
		}

		private async Task<bool> Upload(Stream stream, Uri presignedUri)
		{
			var client = httpClientFactory.CreateClient();
			var response = await client.PutAsync(presignedUri, new StreamContent(stream));
			if (response.IsSuccessStatusCode)
			{
				return true;
			}
			var content = await response.Content.ReadAsStringAsync();
			logger.LogError(content);
			return false;
		}

		private class Screenshot
		{
			public byte[] Bytes { get; set; } = Array.Empty<byte>();
			public string ContentType { get; set; } = "image/png";
		}
	}

	public class ScreenshotOptions
	{
		public string? Format { get; set; }
		public bool FullPage { get; set; }
		public int Height { get; set; }
		public string? Locator { get; set; }
		public int Quality { get; set; }
		public string Url { get; set; } = string.Empty;
		public int Width { get; set; }
	}
}