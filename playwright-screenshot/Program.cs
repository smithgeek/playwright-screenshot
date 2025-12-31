using Microsoft.Playwright;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
var playwright = await Playwright.CreateAsync();
var browser = await playwright.Chromium.LaunchAsync();
builder.Services.AddSingleton(playwright);
builder.Services.AddSingleton(browser);
builder.Services.AddHttpClient();
builder.Services.AddHealthChecks()
	.AddCheck<UpHealthCheck>("Up");

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