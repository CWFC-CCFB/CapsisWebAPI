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
using Capsis.Handler.Main;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace Capsis.Handler
{
    [TestClass]
    public class CapsisHandlerTests
    {

        private static readonly CapsisProcessHandlerSettings Settings = new ("appsettings.json");
        private static readonly ILogger Logger = new LoggerFactory().CreateLogger("CapsisHandler");

        [TestMethod]
        public void TestCapsisHandlerStart()
        {
             try
            {
                CapsisProcessHandler handler = new(Settings, Logger, CapsisProcessHandler.Variant.ARTEMIS);
                handler.Start();
                Assert.AreEqual(false, handler.Process.HasExited);  // make sure the underlying capsis process is alive
                Assert.AreEqual(true, handler.Process.Responding);  // make sure the underlying capsis process is responding
                Assert.AreEqual(CapsisProcessHandler.State.READY, handler.Status);  // make sure the handler is in READY state                    
                handler.Stop();
            }
            catch (Exception ex)
            {
                Assert.IsTrue(false, ex.Message);
            }
        }

        static bool IsProcessAlive(int pid)
        {
            try
            {
                Process process = Process.GetProcessById(pid);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        [TestMethod]
        public void TestCapsisHandlerStartNoStopShouldExitAfterAWhile()
        {
            try
            {
                CapsisProcessHandler handler = new(Settings, Logger, CapsisProcessHandler.Variant.ARTEMIS);
                handler.Start();
                int processID = handler.Process.Id;

                Assert.IsTrue(IsProcessAlive(processID), "The process should exist right now");
                
                handler.stopListening = true; // reinitializing the process member will force the monitoring thread to exit, and thus stop sending STATUS watchdog messages
                Thread.Sleep(30000);

                bool processHasExited = handler.Process.HasExited;
                Assert.IsTrue(processHasExited, "The process should not exist anymore after a while");
                Assert.AreEqual(0, handler.Process.ExitCode, "Process exit code should be 0");  // make sure the underlying capsis process has exited with exit code 0
            }
            catch (Exception ex)
            {
                Assert.IsTrue(false, ex.Message);
            }
        }

        [TestMethod]
        public void TestCapsisHandlerLifeCycleWithSynchronousCall()
        {
            CapsisProcessHandler handler = new(Settings, Logger, CapsisProcessHandler.Variant.ARTEMIS);
            handler.Start();
            List<CapsisProcessHandler.Variant> variantList = CapsisProcessHandler.GetVariantList();
            Assert.IsFalse(variantList.Count == 0);     // make sure the variant list isn't empty
            Assert.AreEqual(false, handler.Process.HasExited, "Process should not have exited yet");  // make sure the underlying capsis process is still alive
            Assert.AreEqual(true, handler.Process.Responding, "Process should be responding");  // make sure the underlying capsis process is still responding            
            Assert.AreEqual(CapsisProcessHandler.State.READY, handler.Status);  // ensure handler is still in READY state (after a synced call)
            handler.Stop();
            Assert.AreEqual(true, handler.Process.HasExited, "Process should have exited by now");  // make sure the underlying capsis process has exited
            Assert.AreEqual(0, handler.Process.ExitCode, "Process exit code should be 0");  // make sure the underlying capsis process has exited with exit code 0
        }

        [TestMethod]
        public void TestCapsisHandlerLifeCycleWithAsynchronousCallHappyPath()
        {
            CapsisProcessHandler handler = new(Settings, Logger, CapsisProcessHandler.Variant.ARTEMIS);
            handler.Start();

            // read the CSV data
            string data = File.ReadAllText("dataTest/STR_RE2_70.csv");
            int[] fieldMatches = { 1, 2, 3, 4, 5, 6, 8, 10, 11, 12, 14, -1, 7, -1, -1, -1, -1, 13, -1 };
            handler.Simulate(data, null, 2000, true, 100, "Stand", "NoChange", 2100, fieldMatches);
            Assert.AreEqual(false, handler.Process.HasExited, "Process should not have exited yet");  // make sure the underlying capsis process is still alive
            Assert.AreEqual(true, handler.Process.Responding, "Process should be responding");  // make sure the underlying capsis process is still responding            
            Assert.AreEqual(CapsisProcessHandler.State.READY, handler.Status);  // ensure handler is still in READY state (after an async call)
            double progress = handler.Progress;
            while (!handler.isResultAvailable())
            {
                double newProgress = handler.Progress;
                Assert.IsTrue(newProgress >= progress, "Progress should always increase only");
                progress = newProgress;
                Thread.Sleep(100);
            }

            Assert.IsTrue(handler.GetResult().Length > 0);

            handler.Stop();
            Assert.AreEqual(true, handler.Process.HasExited, "Process should have exited by now");  // make sure the underlying capsis process has exited
            Assert.AreEqual(0, handler.Process.ExitCode, "Process exit code should be 0");  // make sure the underlying capsis process has exited with exit code 0
        }

        [TestMethod]
        public void TestCapsisHandlerLifeCycleWithAsynchronousCallHappyPath_RE2_120()
        {
            CapsisProcessHandler handler = new(Settings, Logger, CapsisProcessHandler.Variant.ARTEMIS);
            handler.Start();

            // read the CSV data
            string data = File.ReadAllText("dataTest/STR_RE2_120.csv");
            int[] fieldMatches = { 1, 2, 3, 4, 5, 6, 8, 10, 11, 12, 14, -1, 7, -1, -1, -1, -1, 13, -1 };
            handler.Simulate(data, null, 2000, true, 500, "Stand", "NoChange", 2080, fieldMatches);
            //Assert.AreEqual(false, handler.process.HasExited, "Process should not have exited yet");  // make sure the underlying capsis process is still alive
            //Assert.AreEqual(true, handler.process.Responding, "Process should be responding");  // make sure the underlying capsis process is still responding            
            Assert.AreEqual(CapsisProcessHandler.State.READY, handler.Status);  // ensure handler is still in READY state (after an async call)
            double progress = handler.Progress;
            while (!handler.isResultAvailable())
            {
                double newProgress = handler.Progress;
                Assert.IsTrue(newProgress >= progress, "Progress should always increase only");
                progress = newProgress;
                Thread.Sleep(100);
            }

            Assert.IsTrue(handler.GetResult().Length > 0);

            handler.Stop();
            Assert.AreEqual(true, handler.Process.HasExited, "Process should have exited by now");  // make sure the underlying capsis process has exited
            Assert.AreEqual(0, handler.Process.ExitCode, "Process exit code should be 0");  // make sure the underlying capsis process has exited with exit code 0
        }

        [TestMethod]
        public void TestCapsisHandlerLifeCycleWithAsynchronousCallStopBeforeEnd()
        {
            CapsisProcessHandler handler = new(Settings, Logger, CapsisProcessHandler.Variant.ARTEMIS);
            handler.Start();

            // read the CSV data
            string data = File.ReadAllText("dataTest/STR_RE2_70.csv");
            int[] fieldMatches = { 1, 2, 3, 4, 5, 6, 8, 10, 11, 12, 14, -1, 7, -1, -1, -1, -1, 13, -1 };
            handler.Simulate(data, null, 2000, true, 1000, "Stand", "NoChange", 2100, fieldMatches);               
            Assert.AreEqual(false, handler.Process.HasExited, "Process should not have exited yet");  // make sure the underlying capsis process is still alive
            Assert.AreEqual(true, handler.Process.Responding, "Process should be responding");  // make sure the underlying capsis process is still responding            
            Assert.AreEqual(CapsisProcessHandler.State.READY, handler.Status);  // ensure handler is still in READY state (after an async call)
            handler.Stop();
            Assert.AreEqual(true, handler.Process.HasExited, "Process should have exited by now");  // make sure the underlying capsis process has exited
            Assert.AreEqual(0, handler.Process.ExitCode, "Process exit code should be 0");  // make sure the underlying capsis process has exited with exit code 0
        }

        [TestMethod]
        public void TestCapsisHandlerLifeCycleWithAsynchronousCallNoStopShouldExitAfterAWhile()
        {
            CapsisProcessHandler handler = new(Settings, Logger, CapsisProcessHandler.Variant.ARTEMIS);
            handler.Start();
            int processID = handler.Process.Id;

            // read the CSV data
            string data = File.ReadAllText("dataTest/STR_RE2_70.csv");
            int[] fieldMatches = { 1, 2, 3, 4, 5, 6, 8, 10, 11, 12, 14, -1, 7, -1, -1, -1, -1, 13, -1 };
            handler.Simulate(data, null, 2000, true, 200, "Stand", "NoChange", 2100, fieldMatches);

            Assert.IsTrue(IsProcessAlive(processID), "The process should exist right now");

            handler.stopListening = true; // reinitializing the process member will force the monitoring thread to exit, and thus stop sending STATUS watchdog messages
            Thread.Sleep(30000);

            bool processHasExited = handler.Process.HasExited;
            Assert.IsTrue(processHasExited, "The process should not exist anymore after a while");
            Assert.AreEqual(0, handler.Process.ExitCode, "Process exit code should be 0");  // make sure the underlying capsis process has exited with exit code 0
        }

        [TestMethod]
        public void TestCapsisHandlerDefaultOutputRequest()
        {
            CapsisProcessHandler handler = new(Settings, Logger, CapsisProcessHandler.Variant.ARTEMIS);
            handler.Start();

            // read the CSV data
            string data = File.ReadAllText("dataTest/STR_RE2_70.csv");
            int[] fieldMatches = { 1, 2, 3, 4, 5, 6, 8, 10, 11, 12, 14, -1, 7, -1, -1, -1, -1, 13, -1 };
            handler.Simulate(data, null, 2000, true, 100, "Stand", "NoChange", 2100, fieldMatches);
            while (!handler.isResultAvailable())
            {
                Thread.Sleep(100);
            }

            CapsisProcessHandler.SimulationStatus status = handler.GetSimulationStatus();

            Assert.AreEqual(2, status.Result.outputTypes.Count, "Default output request should lead to two outputs");
            Assert.IsTrue(status.Result.outputTypes.Contains("AliveVolume_Broadleaved"), "Default output request should lead to AliveVolume_Broadleaved being output");
            Assert.IsTrue(status.Result.outputTypes.Contains("AliveVolume_Coniferous"), "Default output request should lead to AliveVolume_Coniferous being output");

            handler.Stop();
        }
    }
}