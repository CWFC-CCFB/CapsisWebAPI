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
using CapsisWebAPI.Controllers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using static Capsis.Handler.CapsisProcessHandler;

namespace CapsisWebAPI
{
    [TestClass]
    public class CapsisWebAPITests
    {

        private static ILogger<CapsisSimulationController> logger = new LoggerFactory().CreateLogger<CapsisSimulationController>();


        [TestMethod]
        public void Simulate_HappyPathTest()
        {
            string validOutputRequest = "[{ \"requestType\":{ \"statusClass\":\"Alive\",\"variable\":\"Volume\"},\"aggregationPatterns\":{ \"Coniferous\":[\"EPN\",\"PIG\"]} }]";
            string taskID = StartSimulation(validOutputRequest);
            SimulationStatus simStatus = GetStatus(taskID);
            List<string> outputTypes = simStatus.Result.outputTypes;
            Assert.AreEqual(1, outputTypes.Count);
            Assert.AreEqual("AliveVolume_Coniferous", outputTypes[0]);
        }


        [TestMethod]
        public void Simulate_HappyPathTest2()
        {
            string validOutputRequest = "[{ \"requestType\":{ \"statusClass\":\"Alive\",\"variable\":\"Volume\"},\"aggregationPatterns\":{ \"Coniferous\":[\"EPN\",\"PIG\"], \"SAB\":[\"SAB\"]} }]";
            string taskID = StartSimulation(validOutputRequest);
            SimulationStatus simStatus = GetStatus(taskID);
            List<string> outputTypes = simStatus.Result.outputTypes;
            Assert.AreEqual(2, outputTypes.Count);
            Assert.AreEqual("AliveVolume_Coniferous", outputTypes[0]);
            Assert.AreEqual("AliveVolume_SAB", outputTypes[1]);
        }

        [TestMethod]
        public void Simulate_HappyPathTest3()
        {
            string validOutputRequest = "[{ \"requestType\":{ \"statusClass\":\"Alive\",\"variable\":\"Volume\"},\"aggregationPatterns\":{ \"Coniferous\":[\"EPN\",\"PIG\"], \"SAB\":[\"SAB\"]} }, { \"requestType\":{ \"statusClass\":\"Dead\",\"variable\":\"Volume\"},\"aggregationPatterns\":{ \"SEP\":[\"EPN\",\"SAB\",\"PIG\"]} }]";
            string taskID = StartSimulation(validOutputRequest);
            SimulationStatus simStatus = GetStatus(taskID);
            List<string> outputTypes = simStatus.Result.outputTypes;
            Assert.AreEqual(3, outputTypes.Count);
            Assert.AreEqual("AliveVolume_Coniferous", outputTypes[0]);
            Assert.AreEqual("AliveVolume_SAB", outputTypes[1]);
            Assert.AreEqual("DeadVolume_SEP", outputTypes[2]);
        }



        static string StartSimulation(string outputRequests)
        {
            CapsisSimulationController controller = new(logger);

            string data = File.ReadAllText("data/STR_RE2_70.csv");
            int[] fieldMatches = { 1, 2, 3, 4, 5, 6, 8, 10, 11, 12, 14, -1, 7, -1, -1, -1, -1, 13, -1 };
            string fieldMatchesJSON = JsonConvert.SerializeObject(fieldMatches);

            IActionResult result = controller.Simulate(data, 100, outputRequests, "Artemis", 2000, true, 100, "Stand", "NoChange", fieldMatchesJSON);
            Assert.AreEqual("OkObjectResult", result.GetType().Name, "Did not receive an OkObjectResult result as expected but " + result.GetType().Name);   // OkObjectResult is expected because this call should succeed
            OkObjectResult oresult = (OkObjectResult)result;
            Assert.AreEqual(oresult.StatusCode, 200, "Expected a 200 status code");

            Assert.IsNotNull(oresult.Value);
            string taskID = (string)oresult.Value;
            return taskID;
        }

        static SimulationStatus GetStatus(string taskID) {
            CapsisSimulationController controller = new(logger);
            bool gotResults = false;
            SimulationStatus status;
            IActionResult result;
            OkObjectResult oresult = null;
            while (!gotResults)
            {
                result = controller.SimulationStatus(taskID);
                Assert.AreEqual("OkObjectResult", result.GetType().Name, "Did not receive an OkObjectResult result as expected but " + result.GetType().Name);   // OkObjectResult is expected because this call should succeed
                oresult = (OkObjectResult)result;
                Assert.AreEqual(oresult.StatusCode, 200, "Expected a 200 status code");
                Assert.IsNotNull(oresult.Value);
                status = (CapsisProcessHandler.SimulationStatus)oresult.Value;
                gotResults = status.Status.Equals(CapsisProcessHandler.SimulationStatus.COMPLETED);
                if (!gotResults)
                    Thread.Sleep(100);
            }

            Assert.IsNotNull(oresult.Value);
            SimulationStatus statusToBeReturned = (SimulationStatus)oresult.Value;

            //()oresult.Value.Result.outputTypes.
            // now also check that this task has been removed from the task table
            result = controller.SimulationStatus(taskID);
            Assert.AreEqual("BadRequestObjectResult", result.GetType().Name, "Did not receive a BadRequestObjectResult result as expected but " + result.GetType().Name);   // BadRequestObjectResult is expected because this call should fail
            BadRequestObjectResult BROresult = (BadRequestObjectResult)result;
            Assert.AreEqual(BROresult.StatusCode, 400, "Expected a 400 status code");

            return statusToBeReturned;
        }

        [TestMethod]
        public void Simulate_CancelTest()
        {
            string validOutputRequest = "[{ \"requestType\":{ \"statusClass\":\"Alive\",\"variable\":\"Volume\"},\"aggregationPatterns\":{ \"Coniferous\":[\"EPN\",\"PIG\"]} }]";

            CapsisSimulationController controller = new(logger);

            string data = File.ReadAllText("data/STR_RE2_70.csv");
            int[] fieldMatches = { 1, 2, 3, 4, 5, 6, 8, 10, 11, 12, 14, -1, 7, -1, -1, -1, -1, 13, -1 };
            string fieldMatchesJSON = JsonConvert.SerializeObject(fieldMatches);

            IActionResult result = controller.Simulate(data, 100, validOutputRequest, "Artemis", 2000, true, 100, "Stand", "NoChange", fieldMatchesJSON);
            Assert.AreEqual("OkObjectResult", result.GetType().Name, "Did not receive an OkObjectResult result as expected but " + result.GetType().Name);   // OkObjectResult is expected because this call should succeed
            OkObjectResult oresult = (OkObjectResult)result;
            Assert.AreEqual(oresult.StatusCode, 200, "Expected a 200 status code");

            Assert.IsNotNull(oresult.Value);
            string taskID = (string)oresult.Value;

            Thread.Sleep(2000);

            result = controller.Cancel(taskID);

            Assert.AreEqual("OkResult", result.GetType().Name, "Did not receive an OkResult result as expected but " + result.GetType().Name);   // OkObjectResult is expected because this call should succeed
            OkResult okresult = (OkResult)result;
            Assert.AreEqual(okresult.StatusCode, 200, "Expected a 200 status code");

            // now also check that this task has been removed from the task table
            result = controller.SimulationStatus(taskID);
            Assert.AreEqual("BadRequestObjectResult", result.GetType().Name, "Did not receive a BadRequestObjectResult result as expected but " + result.GetType().Name);   // BadRequestObjectResult is expected because this call should fail
            BadRequestObjectResult BROresult = (BadRequestObjectResult)result;
            Assert.AreEqual(BROresult.StatusCode, 400, "Expected a 400 status code");
        }
    }
}