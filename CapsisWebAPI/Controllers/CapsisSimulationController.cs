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
using Capsis.Handler;
using Capsis.Handler.Requests;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;
using Capsis.Handler.Main;
using static Capsis.Handler.CapsisProcessHandler;
using System.Reflection;

namespace CapsisWebAPI.Controllers
{
    [ApiController]
    [Route("[controller]")]
    [Produces("application/json")]
    public class CapsisSimulationController : ControllerBase
    {               
        private readonly ILogger<CapsisSimulationController> _logger;
        
//        private static readonly string CapsisPath = new ConfigurationBuilder().AddJsonFile("appsettings.json").Build()["CapsisPath"];
//        private static readonly string DataDirectory = new ConfigurationBuilder().AddJsonFile("appsettings.json").Build()["DataDirectory"];
        private static readonly int MaxProcessNumber = int.Parse(new ConfigurationBuilder().AddJsonFile("appsettings.json").Build()["MaxProcessNumber"]);
//        private static readonly int TimeoutMillisec = int.Parse(new ConfigurationBuilder().AddJsonFile("appsettings.json").Build()["TimeoutMillisec"]); 


        static Dictionary<string, CapsisProcessHandler> handlerDict = new Dictionary<string, CapsisProcessHandler>();
        static Dictionary<string, SimulationStatus?> resultDict = new Dictionary<string, SimulationStatus?>();

        static StaticQueryCache? staticQueryCache;

        public static void setStaticQueryCache(StaticQueryCache cache) { staticQueryCache = cache; }

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="logger"> An ILogger instance. It cannot be null.</param>
        public CapsisSimulationController(ILogger<CapsisSimulationController> logger)
        {
            if (logger == null)
                throw new ArgumentNullException("The logger parameter cannot be null!");
            _logger = logger;            
        }

        protected string GetRequestIP(HttpRequest req)
        {
            string ip = "unknown IP";

            if (req.Headers.ContainsKey("X-Forwarded-For"))
            {
                ip = req.Headers["X-Forwarded-For"].ToString();
                if (ip.Contains(':'))
                    ip = ip.Split(':')[0];
            }
            else
            {
                if (req.HttpContext.Connection.RemoteIpAddress != null)
                    ip = req.HttpContext.Connection.RemoteIpAddress.ToString();
            }

            return ip;
        }

        /// <summary>
        /// Logs a request to the log
        /// </summary>
        /// <param name="req">The request object to be logged</param>
        /// <param name="errorMessage">optional errorMessage.  If no error message is provided (null), then the request is considered successful</param>
        protected void LogRequest(HttpRequest req, string? errorMessage = null)
        {
            if (_logger != null)
            {
                if (errorMessage == null)
                    _logger.LogInformation("Success processing request " + req.Method + " " + req.Path + req.QueryString + " from " + GetRequestIP(req));
                else
                    _logger.LogInformation("Error processing request " + req.Method + " " + req.Path + req.QueryString + " from " + GetRequestIP(req) + ".  Message : " + errorMessage);
            }
        }

        protected void processHandlerDict()
        {
            lock (resultDict)
            {
                foreach (KeyValuePair<string, CapsisProcessHandler> entry in handlerDict)
                {
                    CapsisProcessHandler.SimulationStatus status = entry.Value.GetSimulationStatus();
                    resultDict[entry.Key] = status;
                    if (!status.Status.Equals(CapsisProcessHandler.SimulationStatus.IN_PROGRESS))
                    {   // remove the task from the table once the results are being successfully returned                                                            
                        entry.Value.Stop();
                        handlerDict.Remove(entry.Key);                        
                    }
                }
            }
        }

        [HttpGet]
        [Route("VariantList")]
        public IActionResult VariantList()
        {            
            var result = staticQueryCache.VariantDataMap.Keys.ToList();

            if (HttpContext != null)
                LogRequest(HttpContext.Request);

            return Ok(result);   
        }

        /// <summary>
        /// Convert variant string to variant enum.
        /// </summary>
        /// <param name="variantStr"></param>
        /// <returns></returns>
        private static Variant ParseVariant(string variantStr)
        {
            Variant enumVariant = Enum.Parse<Variant>(variantStr.ToUpperInvariant().Trim());
            return enumVariant;
        }

        [HttpGet]
        [Route("VariantSpecies")]
        public IActionResult VariantSpecies([Required][FromQuery] string variant = "Artemis", [FromQuery] string type = "All")
        {            
            try
            {                
                var enumType = Enum.Parse<VariantSpecies.Type>(type);
                Variant variantEnum = ParseVariant(variant);
                var result = staticQueryCache.VariantDataMap[variantEnum].SpeciesMap[enumType];

                if (HttpContext != null)
                    LogRequest(HttpContext.Request);

                return Ok(result);                
            }
            catch (Exception ex)
            {
                if (HttpContext != null)
                    LogRequest(HttpContext.Request, ex.Message);

                return BadRequest(ex.Message);
            }
        }
        
        [HttpGet]
        [Route("OutputRequestTypes")]
        public IActionResult OutputRequestTypes([Required][FromQuery] string variant = "Artemis")
        {
            Variant variantEnum = ParseVariant(variant);
            var result = staticQueryCache.VariantDataMap[variantEnum].Requests;

            if (HttpContext != null)
                LogRequest(HttpContext.Request);

            return Ok(result);
        }
        
        [HttpGet]
        [Route("VariantFields")]
        public IActionResult VariantFields([Required][FromQuery] string variant = "Artemis")
        {            
            try
            {
                Variant variantEnum = ParseVariant(variant);
                var result = staticQueryCache.VariantDataMap[variantEnum].Fields;

                if (HttpContext != null)
                    LogRequest(HttpContext.Request);

                return Ok(result);
            }
            catch (Exception ex)
            {
                if (HttpContext != null)
                    LogRequest(HttpContext.Request, ex.Message);

                return BadRequest(ex.Message);
            }
        }
        
        [HttpPost]
        [Route("Simulate")]
        public IActionResult Simulate([Required][FromForm] String data, [Required][FromQuery] int years, [FromForm] String? output = null, [Required][FromQuery] string variant = "Artemis", [Required][FromQuery] int initialYear = -1, [Required][FromQuery] bool isStochastic = false, [Required][FromQuery] int nbRealizations = 0, [Required][FromQuery] String applicationScale = "Stand", [Required][FromQuery] String climateChange = "NoChange", [Required][FromQuery] string? fieldMatches = null)
        {                        
            try
            {
                lock (handlerDict)
                {
                    processHandlerDict();

                    if (handlerDict.Count >= MaxProcessNumber)
                    {
                        if (HttpContext != null)
                            LogRequest(HttpContext.Request, "Too Many Requests");

                        return StatusCode(429);     // Too Many Requests
                    }

                    List<OutputRequest>? outputRequestList = output == null ? null : Utility.DeserializeObject<List<OutputRequest>>(output);
                    List<int>? fieldMatchesList = fieldMatches == null ? null : Utility.DeserializeObject<List<int>>(fieldMatches);

                    Variant variantEnum = ParseVariant(variant);
                    CapsisProcessHandler handler = new(AppSettings.GetInstance(), _logger, variantEnum);

                    handler.Start();

                    if (initialYear == -1)
                        initialYear = DateTime.Now.Year;

                    Guid newTaskGuid = handler.Simulate(data, outputRequestList, initialYear, isStochastic, nbRealizations, applicationScale, climateChange, initialYear + years, fieldMatchesList == null ? null : fieldMatchesList.ToArray());

                    handlerDict[newTaskGuid.ToString()] = handler;
                    resultDict[newTaskGuid.ToString()] = handler.GetSimulationStatus();

                    if (HttpContext != null)
                        LogRequest(HttpContext.Request);

                    return Ok(newTaskGuid.ToString());
                }
            }
            catch (Exception e)
            {
                if (HttpContext != null)
                    LogRequest(HttpContext.Request, e.Message);

                return BadRequest(e.Message);
            }
        }

        [HttpGet]
        [Route("TaskStatus")]
        public IActionResult SimulationStatus([Required][FromQuery] string taskID)
        {            
            try
            {
                lock (handlerDict)
                {
                    processHandlerDict();
                    
                    CapsisProcessHandler.SimulationStatus status = resultDict[taskID];
                    if (!status.Status.Equals(CapsisProcessHandler.SimulationStatus.IN_PROGRESS))
                    {
                        // remove the task from the table once the results are being successfully returned                                                            
                        resultDict.Remove(taskID);
                    }

                    if (HttpContext != null)
                        LogRequest(HttpContext.Request);

                    return Ok(status);
                }
            }
            catch (Exception)
            {
                string message = "Unrecognized taskID";

                if (HttpContext != null)
                    LogRequest(HttpContext.Request, message);

                return BadRequest(message);
            }
        }

        [HttpGet]
        [Route("Cancel")]
        public IActionResult Cancel([Required][FromQuery] string taskID)
        {            
            try
            {
                lock (handlerDict)
                {
                    processHandlerDict();

                    bool found = false;
                    // at this point, either the taskID is present in the handlerDict (not finished), the resultDict(finished) or not present (invalid)
                    if (handlerDict.ContainsKey(taskID))
                    {
                        handlerDict[taskID].Stop();
                        handlerDict.Remove(taskID);
                        resultDict.Remove(taskID);
                        found = true;
                    }
                    else
                    {
                        if (resultDict.ContainsKey(taskID))
                        {
                            resultDict.Remove(taskID);
                            found = true;
                        }
                    }

                    if (found)
                    {
                        if (HttpContext != null)
                            LogRequest(HttpContext.Request);

                        return Ok();
                    }
                    else
                    {
                        string message = "Unrecognized taskID";

                        if (HttpContext != null)
                            LogRequest(HttpContext.Request, message);

                        return BadRequest(message);
                    }
                }
            }
            catch (Exception e)
            {
                if (HttpContext != null)
                    LogRequest(HttpContext.Request, e.Message);

                return StatusCode(500);
            }
        }

        [HttpGet]
        [Route("")]
        public IActionResult Index()
        {            
            Dictionary<string, object> result = new Dictionary<string, object>();
            result["version"] = Assembly.GetExecutingAssembly().GetName().Version.ToString();

            if (HttpContext != null)
                LogRequest(HttpContext.Request);

            return Ok(result);
        }
    }
}