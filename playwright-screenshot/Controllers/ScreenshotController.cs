using Microsoft.AspNetCore.Mvc;
using Microsoft.Playwright;
using System.Text.Json.Serialization;

namespace playwright_screenshot.Controllers;

public class ScreenshotController(BrowserFactory browserFactory) : Controller
{
	private static int GetMaxParallelRenders()
	{
		if (int.TryParse(Environment.GetEnvironmentVariable("MAX_PARALLEL_RENDERS"), out var max))
		{
			return max;
		}
		return 3;
	}

	private static readonly SemaphoreSlim renderSemaphore = new(GetMaxParallelRenders());

	private static readonly Lazy<string> cryptoPolyfill = new(() =>
	{
		return System.IO.File.ReadAllText("/app/webcrypto-liner.shim.min.js");
	});

	[Route("/screenshot/s3")]
	[HttpPost]
	public async Task<IResult> Post([FromBody] ScreenshotS3Body args)
	{
		if (args.Url == null)
		{
			return TypedResults.BadRequest();
		}
		await renderSemaphore.WaitAsync();
		try
		{
			return await Render(args);
		}
		finally
		{
			renderSemaphore.Release();
		}
	}

	private async Task<IResult> Render(ScreenshotS3Body args)
	{
		await using var contextHolder = await browserFactory.GetContextAsync();
		var context = contextHolder.Context;
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
		if (args.Options.UseCryptoPolyfill)
		{
			Console.WriteLine("Using crypto polyfill");
			await context.AddInitScriptAsync(cryptoPolyfill.Value);
			await context.AddInitScriptAsync(@"() => {
				console.log('Polyfill active. Crypto status:', !!window.crypto.subtle);
			}");
		}
		var page = await context.NewPageAsync();
		try
		{

			PageScreenshotResponse response = new();
			page.Console += (_, msg) =>
			{
				Console.WriteLine($"[CONSOLE {msg.Type.ToUpper()}] {msg.Text}");
				response.Console.Add($"[{msg.Type.ToUpper()}] {msg.Text}");
			};

			if (args.Logging.LogAllRequests)
			{
				page.Request += (_, request) =>
				{
					Console.WriteLine($">> {request.Method} {request.Url}");
				};

				page.Response += (_, response) =>
				{
					if (response.Status >= 400)
					{
						response.TextAsync().ContinueWith(body =>
						{
							Console.WriteLine($"<< {response.Status} {response.Url}: {body}");
						});
					}
					else
					{
						Console.WriteLine($"<< {response.Status} {response.Url}");
					}
				};
			}

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

			await page.GotoAsync(args.Url!);
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
		catch (Exception e)
		{
			Console.WriteLine(e.Message);
			TypedResults.InternalServerError(e);
		}
		finally
		{
			await page.CloseAsync();
		}
		return TypedResults.InternalServerError("Error generating screenshot");
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
	[JsonPropertyName("logging")]
	public LoggingOptions Logging { get; set; } = new();
	public PlaywrightOptions Options { get; set; } = new();

	public class LoggingOptions
	{
		public bool LogAllRequests { get; set; } = false;
	}
	public class PlaywrightOptions
	{
		public bool UseCryptoPolyfill { get; set; } = false;
	}
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