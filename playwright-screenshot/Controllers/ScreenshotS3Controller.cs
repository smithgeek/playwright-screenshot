using Microsoft.AspNetCore.Mvc;
using Microsoft.Playwright;
using System.Text.Json.Serialization;

namespace playwright_screenshot.Controllers;

public class ScreenshotS3Controller(IBrowser browser) : Controller
{
	[Route("/screenshot/s3")]
	[HttpPost]
	public async Task<IResult> Post([FromBody] ScreenshotS3Body args)
	{
		if (args.Url == null)
		{
			return TypedResults.BadRequest();
		}
		await using var context = await browser.NewContextAsync();
		if (args.Cookies.Count > 0)
		{
			await context.AddCookiesAsync(args.Cookies.Select(c => new Cookie
			{
				Domain = c.Domain,
				Path = c.Path,
				Value = c.Value,
				HttpOnly = c.HttpOnly,
				Name = c.Name,
				Secure = c.Secure,
				Url = c.Url,
				Expires = c.Expires,
			}));
		}
		var page = await context.NewPageAsync();
		PageScreenshotResponse response = new();
		page.Console += (_, msg) =>
		{
			Console.WriteLine($"[CONSOLE {msg.Type.ToUpper()}] {msg.Text}");
			response.Console.Add($"[{msg.Type.ToUpper()}] {msg.Text}");
		};

		page.RequestFailed += (_, request) =>
		{
			Console.WriteLine($"[NETWORK ERROR] {request.Method} {request.Url} - {request.Failure}");
			response.NetworkErrors.Add($"{request.Method} {request.Url} - {request.Failure}");
		};

		var onComplete = new TaskCompletionSource<PageScreenshotResponse>();
		await page.ExposeFunctionAsync<object>("onUploadComplete", pageResponse =>
		{
			response.Response = pageResponse;
			onComplete.SetResult(response);
		});
		await page.ExposeFunctionAsync<string>("specialLog", (msg) =>
		{
			Console.WriteLine($"[FROM PAGE] {msg}");
			response.Log.Add(msg);
		});

		await page.GotoAsync(args.Url);
		await Task.WhenAny(onComplete.Task, Task.Delay(TimeSpan.FromSeconds(args.Timeout)));
		if (onComplete.Task.IsCompleted)
		{
			return TypedResults.Ok(onComplete.Task.Result);
		}
		response.Response = new
		{
			Success = false,
			Message = "timeout"
		};
		return TypedResults.Ok(response);
	}
}

public class ScreenshotS3Body
{
	[JsonPropertyName("url")]
	public string? Url { get; set; }
	[JsonPropertyName("timeoutSeconds")]
	public int Timeout { get; set; } = 60;
	[JsonPropertyName("cookies")]
	public List<CookieDto> Cookies { get; set; } = [];
}

public class PageScreenshotResponse
{
	[JsonPropertyName("response")]
	public object? Response { get; set; }
	[JsonPropertyName("console")]
	public List<string> Console { get; init; } = [];
	[JsonPropertyName("networkErrors")]
	public List<string> NetworkErrors { get; init; } = [];
	[JsonPropertyName("log")]
	public List<string> Log { get; init; } = [];
}

public class CookieDto
{
	public required string Name { get; set; }
	public required string Value { get; set; }
	public string? Domain { get; set; }
	public string? Path { get; set; }
	public bool Secure { get; set; }
	public bool HttpOnly { get; set; }
	public string? Url { get; set; }
	public float? Expires { get; set; }
}