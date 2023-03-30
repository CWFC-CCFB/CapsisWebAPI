using Capsis.Handler;
using Capsis.Handler.Requests;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;
using System.Data;
using Microsoft.Extensions.Configuration;
using Capsis.Handler.Main;
using System.Net.NetworkInformation;

namespace CapsisWebAPI.Controllers
{
    [ApiController]
    [Route("[controller]")]
    [Produces("application/json")]
    public class CapsisSimulationController : ControllerBase
    {        
        private readonly ILogger<CapsisSimulationController> _logger;
        
        static readonly string CapsisPath = new ConfigurationBuilder().AddJsonFile("appsettings.json").Build()["CapsisPath"];
        static readonly string DataDirectory = new ConfigurationBuilder().AddJsonFile("appsettings.json").Build()["DataDirectory"];

        Dictionary<string, CapsisProcessHandler> handlerDict;

        public CapsisSimulationController(ILogger<CapsisSimulationController> logger)
        {            
            _logger = logger;
            handlerDict = new Dictionary<string, CapsisProcessHandler>();
        }

        protected void LogRequest(HttpRequest req)
        {
            if (_logger != null)
                _logger.LogInformation("Received request " + req.Method + " " + req.Path + req.QueryString + " from " + req.Headers["Referer"]);
        }

        [HttpGet]
        [Route("VariantList")]
        public IActionResult VariantList()
        {
            CapsisProcessHandler handler = new(CapsisPath, DataDirectory);
            return Ok(handler.VariantList());   // no need to start the process in this particular case since the variant list is not queried from Capsis
        }
        
        [HttpGet]
        [Route("VariantSpecies")]
        public IActionResult VariantSpecies([Required][FromQuery] String variant = "Artemis", [FromQuery] String type = "All")
        {            
            try
            {
                CapsisProcessHandler handler = new(CapsisPath, DataDirectory);
                handler.Start();
                var enumType = Enum.Parse<VariantSpecies.Type>(type);
                return Ok(handler.VariantSpecies(variant, enumType));                
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }
        
        [HttpGet]
        [Route("OutputRequestTypes")]
        public IActionResult OutputRequestTypes()
        {
            CapsisProcessHandler handler = new(CapsisPath, DataDirectory);

            return Ok(handler.OutputRequestTypes());
        }
        
        [HttpGet]
        [Route("VariantFields")]
        public IActionResult VariantFields([Required][FromQuery] String variant = "Artemis")
        {
            CapsisProcessHandler handler = new(CapsisPath, DataDirectory);

            handler.Start();

            try
            {
                return Ok(handler.VariantFieldList(variant));
            }
            catch (Exception ex)
            {
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
                List<OutputRequest>? outputRequestList = output == null ? null : Utility.DeserializeObject<List<OutputRequest>>(output);
                List<int>? fieldMatchesList = fieldMatches == null ? null : Utility.DeserializeObject<List<int>>(fieldMatches);

                CapsisProcessHandler handler = new(CapsisPath, DataDirectory);

                handler.Start();

                if (initialYear == -1)
                    initialYear = DateTime.Now.Year;

                Guid newTaskGuid = handler.Simulate(variant, data, outputRequestList, initialYear, isStochastic, nbRealizations, applicationScale, climateChange, initialYear + years, fieldMatchesList == null ? null : fieldMatchesList.ToArray());

                handlerDict[newTaskGuid.ToString()] = handler;

                return Ok(newTaskGuid.ToString());
            }
            catch (Exception e)
            {
                return BadRequest(e.Message);
            }
        }

        [HttpGet]
        [Route("TaskStatus")]
        public IActionResult SimulationStatus([Required][FromQuery] string taskID)
        {
            try
            {
                CapsisProcessHandler.SimulationStatus status = handlerDict[taskID].GetSimulationStatus();
                if (status.status.Equals(CapsisProcessHandler.SimulationStatus.COMPLETED))
                {   // remove the task from the table once the results are being successfully returned                                                            
                    handlerDict.Remove(taskID);
                }

                return Ok(status);
            }
            catch (Exception e)
            {
                return BadRequest("Unrecognized taskID");
            }
        }

        [HttpGet]
        [Route("TaskCancel")]
        public IActionResult TaskCancel([Required][FromQuery] string taskID)
        {
            try
            {
                handlerDict[taskID].Stop();
                return Ok();
            }
            catch (Exception e)
            {
                return BadRequest("Unrecognized taskID");
            }
        }
    }
}