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
        private static readonly string validOutputRequest = "[{ \"requestType\":{ \"statusClass\":\"Alive\",\"variable\":\"Volume\"},\"aggregationPatterns\":{ \"Coniferous\":[\"EPN\",\"PIG\"]} }]";

        private static ILogger logger = new LoggerFactory().CreateLogger("CapsisHandler");


        [TestMethod]
        public void Simulate_HappyPathTest()
        {
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

            bool gotResults = false;
            while (!gotResults)
            {
                result = controller.SimulationStatus(taskID);
                Assert.AreEqual("OkObjectResult", result.GetType().Name, "Did not receive an OkObjectResult result as expected but " + result.GetType().Name);   // OkObjectResult is expected because this call should succeed
                oresult = (OkObjectResult)result;
                Assert.AreEqual(oresult.StatusCode, 200, "Expected a 200 status code");
                Assert.IsNotNull(oresult.Value);
                CapsisProcessHandler.SimulationStatus status = (CapsisProcessHandler.SimulationStatus)oresult.Value;
                gotResults = status.Status.Equals(CapsisProcessHandler.SimulationStatus.COMPLETED);
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

        [TestMethod]
        public void Simulate_CancelTest()
        {
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