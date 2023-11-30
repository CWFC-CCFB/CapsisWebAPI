using CapsisWebAPI;
using CapsisWebAPI.Controllers;
using NLog.Web;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers().AddNewtonsoftJson();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// NLog: Setup NLog for Dependency injection
builder.Logging.ClearProviders();
builder.Logging.SetMinimumLevel(Microsoft.Extensions.Logging.LogLevel.Trace);
builder.Host.UseNLog();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Logger.LogInformation("CAPSIS path set to " + AppSettings.GetInstance().CapsisDirectory);
app.Logger.LogInformation("DATA path set to " + AppSettings.GetInstance().DataDirectory);

string DataDirectorySweeperMins = new ConfigurationBuilder().AddJsonFile("appsettings.json").Build()["DataDirectorySweeperMins"];
DirectorySweeper sweeper = new DirectorySweeper(AppSettings.GetInstance().DataDirectory, int.Parse(DataDirectorySweeperMins));

CapsisSimulationController.setStaticQueryCache(StaticQueryCache.FillStaticCache(AppSettings.GetInstance(), app.Logger));

app.Run();
