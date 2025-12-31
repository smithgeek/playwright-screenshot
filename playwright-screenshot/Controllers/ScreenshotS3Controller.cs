using Microsoft.AspNetCore.Mvc;
using Microsoft.Playwright;
using System.Text.Json.Serialization;

namespace playwright_screenshot.Controllers;

public class ScreenshotS3Controller(IBrowser browser) : Controller
{
	[Route("/screenshot/s3")]
	public async Task<PageScreenshotResponse> Post([FromBody] ScreenshotS3Body args)
	{
		await using var context = await browser.NewContextAsync();
		var page = await context.NewPageAsync();
		var onComplete = new TaskCompletionSource<PageScreenshotResponse>();
		await page.ExposeFunctionAsync<PageScreenshotResponse>("onUploadComplete", onComplete.SetResult);

		await page.GotoAsync(args.Url);
		await Task.WhenAny(onComplete.Task, Task.Delay(TimeSpan.FromSeconds(args.Timeout)));
		if (onComplete.Task.IsCompleted)
		{
			return onComplete.Task.Result;
		}
		return new() { Success = false, Response = "timeout" };
	}
}

public class ScreenshotS3Body
{
	[JsonPropertyName("url")]
	public required string Url { get; init; }
	[JsonPropertyName("presignedUrl")]
	public required string PresignedUrl { get; init; }
	[JsonPropertyName("timeoutSeconds")]
	public int Timeout { get; init; } = 60;
}

public class PageScreenshotResponse
{
	[JsonPropertyName("success")]
	public bool Success { get; init; }
	[JsonPropertyName("response")]
	public object? Response { get; init; }
}