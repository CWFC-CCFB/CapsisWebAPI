/*
 * This file is part of the CapsisWebAPI solution
 *
 * Author Jean-Francois Lavoie - Canadian Forest Service
 * Copyright (C) 2023 His Majesty the King in Right of Canada
 *
 * This program is free software; you can redistribute it and/or
 * modify it under the terms of the GNU Lesser General Public
 * License as published by the Free Software Foundation; either
 * version 3 of the License, or (at your option) any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied
 * warranty of MERCHANTABILITY or FITNESS FOR A
 * PARTICULAR PURPOSE. See the GNU Lesser General Public
 * License for more details.
 *
 * You should have received a copy of the GNU Lesser General Public
 * License along with this library; if not, write to the Free Software
 * Foundation, Inc., 59 Temple Place, Suite 330, Boston, MA  02111-1307  USA
 */
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


app.Logger.LogInformation("CAPSISWebAPI version " + AppSettings.GetInstance().Version);
app.Logger.LogInformation("CAPSIS path set to " + AppSettings.GetInstance().CapsisDirectory);
app.Logger.LogInformation("DATA path set to " + AppSettings.GetInstance().DataDirectory);

string DataDirectorySweeperMins = new ConfigurationBuilder().AddJsonFile("appsettings.json").Build()["DataDirectorySweeperMins"];
DirectorySweeper sweeper = new DirectorySweeper(AppSettings.GetInstance().DataDirectory, int.Parse(DataDirectorySweeperMins));

CapsisSimulationController.setStaticQueryCache(StaticQueryCache.FillStaticCache(AppSettings.GetInstance(), app.Logger));

app.Run();
