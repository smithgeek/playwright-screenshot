using Microsoft.Playwright;
using playwright_screenshot;
using System.Text.Json;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
using var playwright = await Playwright.CreateAsync();
await using var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
{
	Headless = true,
	Args = [
		"--no-sandbox",
		"--disable-setuid-sandbox",
		"--disable-dev-shm-usage" // Prevents crashes in Docker /dev/shm
    ]
});
builder.Services.AddSingleton(playwright);
builder.Services.AddSingleton(browser);
builder.Services.AddHttpClient();
builder.Services.AddHealthChecks()
	.AddCheck<UpHealthCheck>("Up");

builder.Services.ConfigureHttpJsonOptions(options =>
{
	options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
	options.SerializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
	app.UseSwagger();
	app.UseSwaggerUI();
}

app.UseAuthorization();

app.MapControllers();

app.Run();
