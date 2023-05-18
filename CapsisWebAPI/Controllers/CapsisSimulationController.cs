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
        private readonly ILogger<CapsisSimulationController> _logger;
        
        private static readonly string CapsisPath = new ConfigurationBuilder().AddJsonFile("appsettings.json").Build()["CapsisPath"];
        private static readonly string DataDirectory = new ConfigurationBuilder().AddJsonFile("appsettings.json").Build()["DataDirectory"];
        private static readonly int MaxProcessNumber = int.Parse(new ConfigurationBuilder().AddJsonFile("appsettings.json").Build()["MaxProcessNumber"]);

        static Dictionary<string, CapsisProcessHandler> handlerDict = new Dictionary<string, CapsisProcessHandler>();
        static Dictionary<string, SimulationStatus?> resultDict = new Dictionary<string, SimulationStatus?>();

        static StaticQueryCache? staticQueryCache;

        public static void setStaticQueryCache(StaticQueryCache cache) { staticQueryCache = cache; }

        public CapsisSimulationController(ILogger<CapsisSimulationController> logger)
        {            
            _logger = logger;            
        }

        protected void LogRequest(HttpRequest req)
        {
            if (_logger != null)
                _logger.LogInformation("Received request " + req.Method + " " + req.Path + req.QueryString + " from " + req.Headers["Referer"]);
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
            if (HttpContext != null)
                LogRequest(HttpContext.Request);

            return Ok(staticQueryCache.variantDataMap.Keys.ToList());   
        }
        
        [HttpGet]
        [Route("VariantSpecies")]
        public IActionResult VariantSpecies([Required][FromQuery] String variant = "Artemis", [FromQuery] String type = "All")
        {
            if (HttpContext != null)
                LogRequest(HttpContext.Request);

            try
            {                
                var enumType = Enum.Parse<VariantSpecies.Type>(type);
                return Ok(staticQueryCache.variantDataMap[variant].speciesMap[enumType]);                
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.Message);
                return BadRequest(ex.Message);
            }
        }
        
        [HttpGet]
        [Route("OutputRequestTypes")]
        public IActionResult OutputRequestTypes()
        {
            if (HttpContext != null)
                LogRequest(HttpContext.Request);

            return Ok(staticQueryCache.requestTypes);
        }
        
        [HttpGet]
        [Route("VariantFields")]
        public IActionResult VariantFields([Required][FromQuery] String variant = "Artemis")
        {
            if (HttpContext != null)
                LogRequest(HttpContext.Request);

            try
            {
                return Ok(staticQueryCache.variantDataMap[variant].fields);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.Message);
                return BadRequest(ex.Message);
            }
        }
        
        [HttpPost]
        [Route("Simulate")]
        public IActionResult Simulate([Required][FromForm] String data, [Required][FromQuery] int years, [FromForm] String? output = null, [Required][FromQuery] String variant = "Artemis", [Required][FromQuery] int initialYear = -1, [Required][FromQuery] bool isStochastic = false, [Required][FromQuery] int nbRealizations = 0, [Required][FromQuery] String applicationScale = "Stand", [Required][FromQuery] String climateChange = "NoChange", [Required][FromQuery] string? fieldMatches = null)
        {
            if (HttpContext != null)
                LogRequest(HttpContext.Request);
            
            try
            {
                lock (handlerDict)
                {
                    processHandlerDict();

                    if (handlerDict.Count >= MaxProcessNumber)
                        return StatusCode(429);     // Too Many Requests

                    List<OutputRequest>? outputRequestList = output == null ? null : Utility.DeserializeObject<List<OutputRequest>>(output);
                    List<int>? fieldMatchesList = fieldMatches == null ? null : Utility.DeserializeObject<List<int>>(fieldMatches);

                    CapsisProcessHandler handler = new(CapsisPath, DataDirectory);

                    handler.Start();

                    if (initialYear == -1)
                        initialYear = DateTime.Now.Year;

                    Guid newTaskGuid = handler.Simulate(variant, data, outputRequestList, initialYear, isStochastic, nbRealizations, applicationScale, climateChange, initialYear + years, fieldMatchesList == null ? null : fieldMatchesList.ToArray());

                    handlerDict[newTaskGuid.ToString()] = handler;
                    resultDict[newTaskGuid.ToString()] = handler.GetSimulationStatus();

                    return Ok(newTaskGuid.ToString());
                }
            }
            catch (Exception e)
            {
                _logger.LogError(e.Message);
                return BadRequest(e.Message);
            }
        }

        [HttpGet]
        [Route("TaskStatus")]
        public IActionResult SimulationStatus([Required][FromQuery] string taskID)
        {
            if (HttpContext != null)
                LogRequest(HttpContext.Request);

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
                    
                    return Ok(status);
                }
            }
            catch (Exception e)
            {
                string message = "Unrecognized taskID";
                _logger.LogError(message);
                return BadRequest(message);
            }
        }

        [HttpGet]
        [Route("Cancel")]
        public IActionResult Cancel([Required][FromQuery] string taskID)
        {
            if (HttpContext != null)
                LogRequest(HttpContext.Request);

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
                        return Ok();
                    else
                    {
                        string message = "Unrecognized taskID";
                        _logger.LogError(message);
                        return BadRequest(message);
                    }
                }
            }
            catch (Exception e)
            {
                _logger.LogError(e.Message);
                return StatusCode(500);
            }
        }

        [HttpGet]
        [Route("")]
        public IActionResult Index()
        {
            if (HttpContext != null)
                LogRequest(HttpContext.Request);

            Dictionary<string, object> result = new Dictionary<string, object>();
            result["version"] = Assembly.GetExecutingAssembly().GetName().Version.ToString();
            return Ok(result);
        }
    }
}