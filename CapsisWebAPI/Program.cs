using CapsisWebAPI;
using CapsisWebAPI.Controllers;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers().AddNewtonsoftJson();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

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

string DataDirectory = new ConfigurationBuilder().AddJsonFile("appsettings.json").Build()["DataDirectory"];
string DataDirectorySweeperMins = new ConfigurationBuilder().AddJsonFile("appsettings.json").Build()["DataDirectorySweeperMins"];
DirectorySweeper sweeper = new DirectorySweeper(DataDirectory, int.Parse(DataDirectorySweeperMins));

string CapsisPath = new ConfigurationBuilder().AddJsonFile("appsettings.json").Build()["CapsisPath"];
CapsisWebAPIController.setStaticQueryCache(StaticQueryCache.FillStaticCache(CapsisPath, DataDirectory));

app.Run();
