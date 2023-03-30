using Capsis.Handler;
using CapsisWebAPI.Controllers;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using static Capsis.Handler.CapsisProcessHandler;

namespace CapsisWebAPI
{
    [TestClass]
    public class CapsisWebAPITests
    {
        private static readonly string validOutputRequest = "[{ \"requestType\":{ \"statusClass\":\"Alive\",\"variable\":\"Volume\"},\"aggregationPatterns\":{ \"Coniferous\":[\"EPN\",\"PIG\"]} }]";

        [TestMethod]
        public void Simulate_HappyPathTest()
        {
            CapsisSimulationController controller = new(null);

            string data = File.ReadAllText("data/STR_RE2_70.csv");
            int[] fieldMatches = { 1, 2, 3, 4, 5, 6, 8, 10, 11, 12, 14, -1, 7, -1, -1, -1, -1, 13, -1 };
            string fieldMatchesJSON = JsonConvert.SerializeObject(fieldMatches);

            IActionResult result = controller.Simulate(data, 100, validOutputRequest, "Artemis", 2000, true, 100, "Stand", "NoChange", fieldMatchesJSON);
            Assert.AreEqual("OkObjectResult", result.GetType().Name, "Did not receive an OkObjectResult result as expected but " + result.GetType().Name);   // OkObjectResult is expected because this call should succeed
            OkObjectResult oresult = (OkObjectResult)result;
            Assert.AreEqual(oresult.StatusCode, 200, "Expected a 200 status code");

            Assert.IsNotNull(oresult.Value);
            string taskID = (string)oresult.Value;

            bool gotResults = false;
            while (!gotResults)
            {
                result = controller.SimulationStatus(taskID);
                Assert.AreEqual("OkObjectResult", result.GetType().Name, "Did not receive an OkObjectResult result as expected but " + result.GetType().Name);   // OkObjectResult is expected because this call should succeed
                oresult = (OkObjectResult)result;
                Assert.AreEqual(oresult.StatusCode, 200, "Expected a 200 status code");
                Assert.IsNotNull(oresult.Value);
                CapsisProcessHandler.SimulationStatus status = (CapsisProcessHandler.SimulationStatus)oresult.Value;
                gotResults = status.status.Equals(CapsisProcessHandler.SimulationStatus.COMPLETED);
                if (!gotResults)
                    Thread.Sleep(100);
            }

            Assert.IsNotNull (oresult.Value);

            // now also check that this task has been removed from the task table
            result = controller.SimulationStatus(taskID);
            Assert.AreEqual("BadRequestObjectResult", result.GetType().Name, "Did not receive a BadRequestObjectResult result as expected but " + result.GetType().Name);   // BadRequestObjectResult is expected because this call should fail
            BadRequestObjectResult BROresult = (BadRequestObjectResult)result;
            Assert.AreEqual(BROresult.StatusCode, 400, "Expected a 400 status code");
        }
    }
}