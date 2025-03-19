/*
 * This file is part of the CapsisWebAPI solution
 *
 * Authors: Jean-Francois Lavoie and Mathieu Fortin, Canadian Forest Service
 * Copyright (C) 2023-25 His Majesty the King in Right of Canada
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
using static Capsis.Handler.CapsisProcessHandler;

namespace Capsis.Handler
{
    public abstract class AbstractCapsisHandlerTests
    {

        private static readonly CapsisProcessHandlerSettings Settings = new ("appsettings.json");
        private static readonly ILogger Logger = new LoggerFactory().CreateLogger("CapsisHandler");

        protected abstract CapsisProcessHandler.Variant GetVariant();

        [TestMethod]
        public void Test01CapsisHandlerStart()
        {
            CapsisProcessHandler handler = new(Settings, Logger, GetVariant());
            handler.Start();
            Assert.AreEqual(false, handler.Process.HasExited);  // make sure the underlying capsis process is alive
            Assert.AreEqual(true, handler.Process.Responding);  // make sure the underlying capsis process is responding
            Assert.AreEqual(CapsisProcessHandler.State.READY, handler.Status);  // make sure the handler is in READY state                    
            handler.Stop();
            Thread.Sleep(1000);
            Assert.AreEqual(true, handler.Process.HasExited, "Process should have exited by now");  // make sure the underlying capsis process has exited
            Assert.AreEqual(0, handler.Process.ExitCode, "Process exit code should be 0");  // make sure the underlying capsis process has exited with exit code 0
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
        public void Test02CapsisHandlerStartNoStopShouldExitAfterAWhile()
        {
            try
            {
                CapsisProcessHandler handler = new(Settings, Logger, GetVariant());
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
        public void Test03CapsisHandlerLifeCycleWithSynchronousCall()
        {
            CapsisProcessHandler handler = new(Settings, Logger, GetVariant());
            handler.Start();
            List<CapsisProcessHandler.Variant> variantList = CapsisProcessHandler.GetVariantList();
            Assert.IsFalse(variantList.Count == 0);     // make sure the variant list isn't empty
            Assert.AreEqual(false, handler.Process.HasExited, "Process should not have exited yet");  // make sure the underlying capsis process is still alive
            Assert.AreEqual(true, handler.Process.Responding, "Process should be responding");  // make sure the underlying capsis process is still responding            
            Assert.AreEqual(CapsisProcessHandler.State.READY, handler.Status);  // ensure handler is still in READY state (after a synced call)
            handler.Stop();
            Thread.Sleep(1000);
            Assert.AreEqual(true, handler.Process.HasExited, "Process should have exited by now");  // make sure the underlying capsis process has exited
            Assert.AreEqual(0, handler.Process.ExitCode, "Process exit code should be 0");  // make sure the underlying capsis process has exited with exit code 0
        }

        [TestMethod]
        public void Test04CapsisHandlerLifeCycleWithAsynchronousCallHappyPath()
        {
            CapsisProcessHandler handler = new(Settings, Logger, GetVariant());
            handler.Start();

            // read the CSV data
            string data = File.ReadAllText("dataTest/STR_RE2_70.csv");
            Dictionary<string, string> fieldMatches = GetFieldMatches();

            handler.Simulate(data, null, 2000, true, 100, "Stand", "NoChange", 2100, fieldMatches);
            Assert.AreEqual(false, handler.Process.HasExited, "Process should not have exited yet");  // make sure the underlying capsis process is still alive
            Assert.AreEqual(true, handler.Process.Responding, "Process should be responding");  // make sure the underlying capsis process is still responding            
            Assert.AreEqual(CapsisProcessHandler.State.READY, handler.Status);  // ensure handler is still in READY state (after an async call)
            var statusSim = handler.GetSimulationStatus();

            double progress = handler.Progress;
            SimulationStatus simStatus = handler.GetSimulationStatus();
            while (simStatus.Status == SimulationStatus.IN_PROGRESS)
            {
                double newProgress = handler.Progress;
                Assert.IsTrue(newProgress >= progress, "Progress should always increase only");
                progress = newProgress;
                Thread.Sleep(100);
                simStatus = handler.GetSimulationStatus();
            }

            Assert.IsTrue(simStatus.Result != null);

            handler.Stop();
            Assert.AreEqual(true, handler.Process.HasExited, "Process should have exited by now");  // make sure the underlying capsis process has exited
            Assert.AreEqual(0, handler.Process.ExitCode, "Process exit code should be 0");  // make sure the underlying capsis process has exited with exit code 0
        }

        [TestMethod]
        public void Test05CapsisHandlerLifeCycleWithAsynchronousCallHappyPath_RE2_120()
        {
            CapsisProcessHandler handler = new(Settings, Logger, GetVariant());
            handler.Start();

            // read the CSV data
            string data = File.ReadAllText("dataTest/STR_RE2_120.csv");
            Dictionary<string, string> fieldMatches = GetFieldMatches();

            handler.Simulate(data, null, 2000, true, 500, "Stand", "NoChange", 2080, fieldMatches);
            Assert.AreEqual(false, handler.Process.HasExited, "Process should not have exited yet");  // make sure the underlying capsis process is still alive
            Assert.AreEqual(true, handler.Process.Responding, "Process should be responding");  // make sure the underlying capsis process is still responding            
            Assert.AreEqual(CapsisProcessHandler.State.READY, handler.Status);  // ensure handler is still in READY state (after an async call)
            double progress = handler.Progress;
            SimulationStatus simStatus = handler.GetSimulationStatus();
            while (simStatus.Status == SimulationStatus.IN_PROGRESS)
            {
                double newProgress = handler.Progress;
                Assert.IsTrue(newProgress >= progress, "Progress should always increase only");
                progress = newProgress;
                Thread.Sleep(100);
                simStatus = handler.GetSimulationStatus();
            }

            Assert.IsTrue(simStatus.Result != null);

            handler.Stop();
            Assert.AreEqual(true, handler.Process.HasExited, "Process should have exited by now");  // make sure the underlying capsis process has exited
            Assert.AreEqual(0, handler.Process.ExitCode, "Process exit code should be 0");  // make sure the underlying capsis process has exited with exit code 0
        }

        [TestMethod]
        public void Test06CapsisHandlerLifeCycleWithAsynchronousCallStopBeforeEnd()
        {
            CapsisProcessHandler handler = new(Settings, Logger, GetVariant());
            handler.Start();

            // read the CSV data
            string data = File.ReadAllText("dataTest/STR_RE2_70.csv");
            Dictionary<string, string> fieldMatches = GetFieldMatches();
            handler.Simulate(data, null, 2000, true, 1000, "Stand", "NoChange", 2100, fieldMatches);               
            Assert.AreEqual(false, handler.Process.HasExited, "Process should not have exited yet");  // make sure the underlying capsis process is still alive
            Assert.AreEqual(true, handler.Process.Responding, "Process should be responding");  // make sure the underlying capsis process is still responding            
            Assert.AreEqual(CapsisProcessHandler.State.READY, handler.Status);  // ensure handler is still in READY state (after an async call)
            while(handler.Progress <= 0.01)
            {
                Console.WriteLine("Progress " + handler.Progress);
                Thread.Sleep(1000);
            }
            Console.WriteLine("Progress " + handler.Progress);
            handler.Stop();
            Thread.Sleep(1000);
            Assert.AreEqual(true, handler.Process.HasExited, "Process should have exited by now");  // make sure the underlying capsis process has exited
            Assert.AreEqual(0, handler.Process.ExitCode, "Process exit code should be 0");  // make sure the underlying capsis process has exited with exit code 0
        }

        [TestMethod]
        public void Test07CapsisHandlerLifeCycleWithAsynchronousCallNoStopShouldExitAfterAWhile()
        {
            CapsisProcessHandler handler = new(Settings, Logger, GetVariant());
            handler.Start();
            int processID = handler.Process.Id;

            // read the CSV data
            string data = File.ReadAllText("dataTest/STR_RE2_70.csv");
            Dictionary<string, string> fieldMatches = GetFieldMatches();

            handler.Simulate(data, null, 2000, true, 200, "Stand", "NoChange", 2100, fieldMatches);

            Assert.IsTrue(IsProcessAlive(processID), "The process should exist right now");

            handler.stopListening = true; // reinitializing the process member will force the monitoring thread to exit, and thus stop sending STATUS watchdog messages
            Thread.Sleep(30000);

            bool processHasExited = handler.Process.HasExited;
            Assert.IsTrue(processHasExited, "The process should not exist anymore after a while");
            Assert.AreEqual(0, handler.Process.ExitCode, "Process exit code should be 0");  // make sure the underlying capsis process has exited with exit code 0
        }



        static Dictionary<string, string> GetFieldMatches()
        {
            Dictionary<string, string> fieldMatches = new Dictionary<string, string>();
            fieldMatches["PLOT"] = "ID_PE";
            fieldMatches["LATITUDE"] = "LATITUDE";
            fieldMatches["LONGITUDE"] = "LONGITUDE";
            fieldMatches["ALTITUDE"] = "ALTITUDE";
            fieldMatches["ECOREGION"] = "GUIDE_ECO";
            fieldMatches["TYPEECO"] = "TYPE_ECO";
            fieldMatches["DRAINAGE_CLASS"] = "CL_DRAI";
            fieldMatches["SPECIES"] = "ESSENCE";
            fieldMatches["TREESTATUS"] = "ETAT";
            fieldMatches["TREEDHPCM"] = "dbhCm";
            fieldMatches["TREEFREQ"] = "freq";
            fieldMatches["TREEHEIGHT"] = "heightM";
            fieldMatches["SLOPE_CLASS"] = "CL_PENT";

            return fieldMatches;
        }

        [TestMethod]
        public void Test08CapsisHandlerDefaultOutputRequest()
        {
            CapsisProcessHandler handler = new(Settings, Logger, GetVariant());
            handler.Start();

            // read the CSV data
            string data = File.ReadAllText("dataTest/STR_RE2_70.csv");
            Dictionary<string, string> fieldMatches = GetFieldMatches();
            handler.Simulate(data, null, 2000, true, 1000, "Stand", "NoChange", 2050, fieldMatches);
            SimulationStatus simStatus = handler.GetSimulationStatusAfterCompletion();

            Assert.AreEqual(1, simStatus.Result.outputTypes.Count, "Default output request should lead to alive volume outputs");
            Assert.IsTrue(simStatus.Result.outputTypes.Contains("AliveVolume_AllSpecies"), "Default output request should lead to AliveVolume_Broadleaved being output");

            handler.Stop();
            Thread.Sleep(1000);
            Assert.AreEqual(true, handler.Process.HasExited, "Process should have exited by now");  // make sure the underlying capsis process has exited
            Assert.AreEqual(0, handler.Process.ExitCode, "Process exit code should be 0");  // make sure the underlying capsis process has exited with exit code 0
        }

        [TestMethod]
        public void Test09CapsisHandlerWithSimulationFailingOnStart()
        {
            CapsisProcessHandler handler = new(Settings, Logger, GetVariant());
            handler.Start();

            // read the CSV data
            string data = File.ReadAllText("dataTest/STR_RE2_70.csv");
            Dictionary<string, string> fieldMatches = GetFieldMatches();
            fieldMatches["PLOT"] = "ID";    // we set the wrong field name here.
            handler.Simulate(data, null, 2000, true, 100, "Stand", "NoChange", 2100, fieldMatches);

            SimulationStatus simStatus = handler.GetSimulationStatusAfterCompletion();
            Assert.AreEqual(SimulationStatus.ERROR, simStatus.Status);
            handler.Stop();
            Thread.Sleep(1000);
            Assert.AreEqual(true, handler.Process.HasExited, "Process should have exited by now");  // make sure the underlying capsis process has exited
            Assert.AreEqual(0, handler.Process.ExitCode, "Process exit code should be 0");  // make sure the underlying capsis process has exited with exit code 0
        }

    }
}