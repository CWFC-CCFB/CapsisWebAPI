/*
 * This file is part of the CapsisWebAPI solution
 *
 * Copyright (C) 2023-25 His Majesty the King in Right of Canada
 * Authors: Jean-Francois Lavoie and Mathieu Fortin, Canadian Forest Service
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
using NLog;
using NLog.Web;
using WebAPIUtilities.ClientInteraction;

namespace CapsisWebAPI
{
    public class Program
    {
        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureLogging((hostContext, logging) =>
                {
                    logging.ClearProviders();
                    logging.SetMinimumLevel(Microsoft.Extensions.Logging.LogLevel.Trace);
                }).UseNLog().ConfigureServices(services =>
                {
                }).ConfigureAppConfiguration((HostContext, config) =>
                {
                    config.AddEnvironmentVariables(prefix: "CAPSIS_");
                }).ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder.UseStartup<Startup>();
                });
        
        public static void Main(string[] args)
        {
            var nLogger = NLog.LogManager.Setup().LoadConfigurationFromAppSettings().GetCurrentClassLogger();


            try
            {
                var app = CreateHostBuilder(args).Build();
                Microsoft.Extensions.Logging.ILogger logger = app.Services.GetRequiredService<Microsoft.Extensions.Logging.ILogger<CapsisSimulationController>>();
                logger.LogInformation($"CapsisWebAPI {AppSettings.GetInstance().Version} initializing...");
                logger.LogInformation("CAPSIS path set to " + AppSettings.GetInstance().CapsisDirectory);
                logger.LogInformation("DATA path set to " + AppSettings.GetInstance().DataDirectory);
                new ReadLatestClientVersionTask(logger, 60);
                new ReadLatestMessageToClientTask(logger, 60);
                new ReadShortLicenseTask(logger, 60);
                CapsisSimulationController.setStaticQueryCache(StaticQueryCache.FillStaticCache(AppSettings.GetInstance(), logger));
                app.Run();
            }
            catch (Exception e)
            {
                nLogger.Error(e, "Application stopped because of exception");
            }
            finally
            {
                NLog.LogManager.Shutdown();
            }
        }
    }



//    // Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
//    builder.Services.AddEndpointsApiExplorer();
//builder.Services.AddSwaggerGen();


//var app = builder.Build();

//// Configure the HTTP request pipeline.
//if (app.Environment.IsDevelopment())
//{
//    app.UseSwagger();
//    app.UseSwaggerUI();
//}

//app.UseHttpsRedirection();




//string DataDirectorySweeperMins = new ConfigurationBuilder().AddJsonFile("appsettings.json").Build()["DataDirectorySweeperMins"];
//DirectorySweeper sweeper = new DirectorySweeper(AppSettings.GetInstance().DataDirectory, int.Parse(DataDirectorySweeperMins));

//CapsisSimulationController.setStaticQueryCache(StaticQueryCache.FillStaticCache(AppSettings.GetInstance(), app.Logger));


//app.Run();

}
