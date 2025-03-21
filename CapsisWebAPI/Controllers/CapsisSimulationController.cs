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
using WebAPIUtilities.Controllers;

namespace CapsisWebAPI.Controllers
{




    [ApiController]
    [Route("[controller]")]
    [Produces("application/json")]
    public class CapsisSimulationController : AbstractWebAPIController
    {               
        
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
        /// <param name="logger"> An ILogger instance.</param>
        public CapsisSimulationController(ILogger<CapsisSimulationController> logger) : base(logger) {}


        protected override Dictionary<string, string> GetStatusDictionary(string? clientversion)
        {
            Dictionary<string, string> outputDict = base.GetStatusDictionary(clientversion);
            outputDict["Capsis version"] = staticQueryCache.CAPSISVersion;
            return outputDict;
        }

        /// <summary>
        /// Update the handler and result dictionaries.
        /// </summary>
        /// <remarks>
        /// The handler status is stored in the result dictionary. If this status is different from "IN PROGRESS",
        /// the handler is removed from the handler dictionary.
        /// </remarks>
        protected void ProcessHandlerDict()
        {
            lock (resultDict)
            {
                foreach (KeyValuePair<string, CapsisProcessHandler> entry in handlerDict)
                {
                    CapsisProcessHandler.SimulationStatus status = entry.Value.GetSimulationStatus();
                    resultDict[entry.Key] = status;
                    if (!status.Status.Equals(CapsisProcessHandler.SimulationStatus.IN_PROGRESS))
                    {   // remove the task from the table once the results are being successfully returned
                        if (status.Status.Equals(CapsisProcessHandler.SimulationStatus.ERROR))
                            GetLogger().LogError($"Handler {entry.Key} failed with error message: {entry.Value.ErrorMessage}");
                        GetLogger().LogInformation($"Removing handler id {entry.Key} with status {status.Status} from handler dictionary.");
                        entry.Value.Stop(); // request the handler to stop
                        handlerDict.Remove(entry.Key); // remove the entry from the handler dictionary                        
                    }
                }
            }
        }

  

        [HttpGet]
        [Route("VariantList")]
        public IActionResult VariantList()
        {            
            List<Variant> result = staticQueryCache.VariantDataMap.Keys.ToList();
            List<string> variantStrings = result.Select(x => Enum.GetName(x)).ToList();

            if (HttpContext != null)
                LogRequest(HttpContext.Request);

            return Ok(variantStrings);   
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
        [Route("VariantScope")]
        public IActionResult VariantScope([Required][FromQuery] string variant = "Artemis")
        {
            try
            {
                Variant variantEnum = ParseVariant(variant);
                var result = staticQueryCache.VariantDataMap[variantEnum].Scope;

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
                    ProcessHandlerDict();

                    if (handlerDict.Count >= MaxProcessNumber)
                    {
                        if (HttpContext != null)
                            LogRequest(HttpContext.Request, "Too Many Requests");

                        return StatusCode(429);     // Too Many Requests
                    }

                    List<OutputRequest>? outputRequestList = output == null ? null : Utility.DeserializeObject<List<OutputRequest>>(output);
                    Dictionary<string, string>? fieldMatchesList = fieldMatches == null ? 
                        null : 
                        Utility.DeserializeObject<Dictionary<string, string>>(fieldMatches);

                    Variant variantEnum = ParseVariant(variant);
                    CapsisProcessHandler handler = new(AppSettings.GetInstance(), 
                        GetLogger(), 
                        variantEnum);

                    handler.Start();

                    if (initialYear == -1)
                        initialYear = DateTime.Now.Year;

                    Guid newTaskGuid = handler.Simulate(data, 
                        outputRequestList, 
                        initialYear, 
                        isStochastic, 
                        nbRealizations, 
                        applicationScale, 
                        climateChange, 
                        initialYear + years, 
                        fieldMatchesList);

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
                    ProcessHandlerDict(); // we update the result dictionary first
                    
                    CapsisProcessHandler.SimulationStatus status = resultDict[taskID];
                    if (!status.Status.Equals(CapsisProcessHandler.SimulationStatus.IN_PROGRESS))
                    {
                        GetLogger().LogInformation($"Task {taskID} is no longer in progress and therefore it will be removed from the result dictionary.");
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
                    ProcessHandlerDict();

                    bool found = false;
                    // at this point, either the taskID is present in the handlerDict (not finished), the resultDict(finished) or not present (invalid)
                    if (handlerDict.ContainsKey(taskID))
                    {
                        GetLogger().LogInformation($"Cancelling unfinished task {taskID} and removing it from both handler and result dictionaries.");
                        handlerDict[taskID].Stop();
                        handlerDict.Remove(taskID);
                        resultDict.Remove(taskID);
                        found = true;
                    }
                    else
                    {
                        if (resultDict.ContainsKey(taskID))
                        {
                            GetLogger().LogInformation($"Removing finished task {taskID} from both result dictionary.");
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
                        GetLogger().LogWarning($"Task {taskID} has not been found in either the handler or the result dictionary. Therefore, it cannot be cancelled!");
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

        protected override string GetClientPackageName()
        {
            return "CapsisWebAPI4R";
        }

        protected override string GetClientPackageURL()
        {
            return "https://github.com/CWFC-CCFB/CapsisWebAPI4R";
        }

        protected override string GetWebAPIVersion()
        {
            return AppSettings.GetInstance().Version;
        }
    }
}