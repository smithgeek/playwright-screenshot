using Microsoft.Playwright;

namespace playwright_screenshot;

public sealed class ContextWrapper : IAsyncDisposable
{
	private readonly BrowserHolder holder;
	public IBrowserContext Context { get; }

	internal ContextWrapper(IBrowserContext context, BrowserHolder holder)
	{
		Context = context;
		this.holder = holder;
	}

	public async ValueTask DisposeAsync()
	{
		await Context.DisposeAsync();
		holder.FinishRender();
	}
}

public sealed class BrowserHolder(IPlaywright playwright, IBrowser browser)
{
	private int renderCount;
	private int activeRenders;
	private bool retiring;

	public static async Task<BrowserHolder> CreateAsync()
	{
		var playwright = await Playwright.CreateAsync();

		var browser = await playwright.Chromium.LaunchAsync(new()
		{
			Headless = true,
			Args =
			[
				"--headless=new",
				"--no-sandbox",
				"--disable-setuid-sandbox",
				"--disable-dev-shm-usage", // Prevents crashes in Docker /dev/shm
				"--disable-background-networking",
				"--disable-background-timer-throttling",
				"--disable-backgrounding-occluded-windows",
				"--disable-breakpad",
				"--disable-component-update",
				"--disable-default-apps",
				"--disable-features=Translate,BackForwardCache",
				"--disable-hang-monitor",
				"--disable-sync",
				"--disable-extensions",
				"--metrics-recording-only",
				"--mute-audio"
			]
		});

		return new BrowserHolder(playwright, browser);
	}

	public bool IsRetiring => retiring;
	public bool IsIdle => Volatile.Read(ref activeRenders) == 0;

	public int IncrementRenderCount()
		=> Interlocked.Increment(ref renderCount);

	public async Task<ContextWrapper> CreateContextAsync()
	{
		Interlocked.Increment(ref activeRenders);

		var context = await browser.NewContextAsync(new()
		{
			ViewportSize = new() { Width = 1920, Height = 1080 },
			DeviceScaleFactor = 1
		});

		return new ContextWrapper(context, this);
	}

	public void FinishRender()
	{
		Interlocked.Decrement(ref activeRenders);
	}

	public void MarkRetiring()
	{
		retiring = true;
	}

	public async Task DisposeAsync()
	{
		await browser.CloseAsync();
		playwright.Dispose();
	}
}




public sealed class BrowserFactory : IAsyncDisposable
{
	private const int MaxRendersPerBrowser = 300;

	private readonly SemaphoreSlim renderLock = new(1, 1);
	private BrowserHolder? current;
	private readonly List<BrowserHolder> retiring = [];

	public async Task<ContextWrapper> GetContextAsync()
	{
		await renderLock.WaitAsync();
		try
		{
			current ??= await BrowserHolder.CreateAsync();

			var renderCount = current.IncrementRenderCount();

			if (renderCount >= MaxRendersPerBrowser)
			{
				current.MarkRetiring();
				retiring.Add(current);
				current = await BrowserHolder.CreateAsync();
			}

			return await current.CreateContextAsync();
		}
		finally
		{
			renderLock.Release();
			_ = CleanupAsync(); // fire-and-forget, but safe
		}
	}

	private async Task CleanupAsync()
	{
		List<BrowserHolder>? toDispose = null;

		await renderLock.WaitAsync();
		try
		{
			toDispose = retiring
				.Where(b => b.IsIdle)
				.ToList();

			foreach (var b in toDispose)
			{
				retiring.Remove(b);
			}
		}
		finally
		{
			renderLock.Release();
		}

		if (toDispose != null)
		{
			foreach (var b in toDispose)
			{
				await b.DisposeAsync();
			}
		}
	}

	public async ValueTask DisposeAsync()
	{
		await renderLock.WaitAsync();
		try
		{
			if (current != null)
			{
				await current.DisposeAsync();
			}

			foreach (var b in retiring)
			{
				await b.DisposeAsync();
			}

			retiring.Clear();
		}
		finally
		{
			renderLock.Release();
			renderLock.Dispose();
		}
	}
}
