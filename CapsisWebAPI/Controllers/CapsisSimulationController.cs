using Capsis.Handler;
using Capsis.Handler.Requests;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;
using System.Data;
using Microsoft.Extensions.Configuration;
using Capsis.Handler.Main;
using System.Net.NetworkInformation;
using System.Threading.Tasks;
using static Capsis.Handler.CapsisProcessHandler;
using System.Reflection;

namespace CapsisWebAPI.Controllers
{
    [ApiController]
    [Route("[controller]")]
    [Produces("application/json")]
    public class CapsisSimulationController : ControllerBase
    {               
        private readonly ILogger<CapsisSimulationController>? _logger;
        
        private static readonly string CapsisPath = new ConfigurationBuilder().AddJsonFile("appsettings.json").Build()["CapsisPath"];
        private static readonly string DataDirectory = new ConfigurationBuilder().AddJsonFile("appsettings.json").Build()["DataDirectory"];
        private static readonly int MaxProcessNumber = int.Parse(new ConfigurationBuilder().AddJsonFile("appsettings.json").Build()["MaxProcessNumber"]);

        static Dictionary<string, CapsisProcessHandler> handlerDict = new Dictionary<string, CapsisProcessHandler>();
        static Dictionary<string, SimulationStatus?> resultDict = new Dictionary<string, SimulationStatus?>();

        static StaticQueryCache? staticQueryCache;

        public static void setStaticQueryCache(StaticQueryCache cache) { staticQueryCache = cache; }

        public CapsisSimulationController(ILogger<CapsisSimulationController>? logger)
        {            
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
                    if (status.status.Equals(CapsisProcessHandler.SimulationStatus.COMPLETED))
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
            var result = staticQueryCache.variantDataMap.Keys.ToList();

            if (HttpContext != null)
                LogRequest(HttpContext.Request);

            return Ok(result);   
        }
        
        [HttpGet]
        [Route("VariantSpecies")]
        public IActionResult VariantSpecies([Required][FromQuery] String variant = "Artemis", [FromQuery] String type = "All")
        {            
            try
            {                
                var enumType = Enum.Parse<VariantSpecies.Type>(type);

                var result = staticQueryCache.variantDataMap[variant].speciesMap[enumType];

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
        public IActionResult OutputRequestTypes()
        {
            var result = staticQueryCache.requestTypes;

            if (HttpContext != null)
                LogRequest(HttpContext.Request);

            return Ok(result);
        }
        
        [HttpGet]
        [Route("VariantFields")]
        public IActionResult VariantFields([Required][FromQuery] String variant = "Artemis")
        {            
            try
            {
                var result = staticQueryCache.variantDataMap[variant].fields;

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
        public IActionResult Simulate([Required][FromForm] String data, [Required][FromQuery] int years, [FromForm] String? output = null, [Required][FromQuery] String variant = "Artemis", [Required][FromQuery] int initialYear = -1, [Required][FromQuery] bool isStochastic = false, [Required][FromQuery] int nbRealizations = 0, [Required][FromQuery] String applicationScale = "Stand", [Required][FromQuery] String climateChange = "NoChange", [Required][FromQuery] string? fieldMatches = null)
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

                    CapsisProcessHandler handler = new(CapsisPath, DataDirectory);

                    handler.Start();

                    if (initialYear == -1)
                        initialYear = DateTime.Now.Year;

                    Guid newTaskGuid = handler.Simulate(variant, data, outputRequestList, initialYear, isStochastic, nbRealizations, applicationScale, climateChange, initialYear + years, fieldMatchesList == null ? null : fieldMatchesList.ToArray());

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
                    if (status.status.Equals(CapsisProcessHandler.SimulationStatus.COMPLETED))
                    {
                        // remove the task from the table once the results are being successfully returned                                                            
                        resultDict.Remove(taskID);
                    }

                    if (HttpContext != null)
                        LogRequest(HttpContext.Request);

                    return Ok(status);
                }
            }
            catch (Exception e)
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