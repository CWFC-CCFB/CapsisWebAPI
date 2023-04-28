using Microsoft.Extensions.Configuration;
using System.Data;
using System.Diagnostics;

namespace Capsis.Handler
{
    [TestClass]
    public class CapsisHandlerTests
    {        
        static string getCapsisPath() { return new ConfigurationBuilder().AddJsonFile("appsettings.json").Build()["CapsisPath"]; }
        static string getDataDirectory() { return new ConfigurationBuilder().AddJsonFile("appsettings.json").Build()["DataDirectory"]; }

        [TestMethod]
        public void TestCapsisHandlerStart()
        {
            try
            {
                CapsisProcessHandler handler = new(getCapsisPath(), getDataDirectory());
                handler.Start();
                Assert.AreEqual(false, handler.process.HasExited);  // make sure the underlying capsis process is alive
                Assert.AreEqual(true, handler.process.Responding);  // make sure the underlying capsis process is responding
                Assert.AreEqual(CapsisProcessHandler.State.READY, handler.getState());  // make sure the handler is in READY state                    
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
            catch (Exception ex)
            {
                return false;
            }
        }

        [TestMethod]
        public void TestCapsisHandlerStartNoStopShouldExitAfterAWhile()
        {
            try
            {
                CapsisProcessHandler handler = new(getCapsisPath(), getDataDirectory());
                handler.Start();
                int processID = handler.process.Id;

                Assert.IsTrue(IsProcessAlive(processID), "The process should exist right now");
                
                handler.process = null; // reinitializing the process member will force the monitoring thread to exit, and thus stop sending STATUS watchdog messages
                Thread.Sleep(12000);

                Assert.IsTrue(IsProcessAlive(processID), "The process should not exist anymore after a while");
            }
            catch (Exception ex)
            {
                Assert.IsTrue(false, ex.Message);
            }
        }

        [TestMethod]
        public void TestCapsisHandlerLifeCycleWithSynchronousCall()
        {
            CapsisProcessHandler handler = new(getCapsisPath(), getDataDirectory());
            handler.Start();
            List<string> variantList = handler.VariantList();
            Assert.IsFalse(variantList.Count == 0);     // make sure the variant list isn't empty
            Assert.AreEqual(false, handler.process.HasExited, "Process should not have exited yet");  // make sure the underlying capsis process is still alive
            Assert.AreEqual(true, handler.process.Responding, "Process should be responding");  // make sure the underlying capsis process is still responding            
            Assert.AreEqual(CapsisProcessHandler.State.READY, handler.getState());  // ensure handler is still in READY state (after a synced call)
            handler.Stop();
            Assert.AreEqual(true, handler.process.HasExited, "Process should have exited by now");  // make sure the underlying capsis process has exited
            Assert.AreEqual(0, handler.process.ExitCode, "Process exit code should be 0");  // make sure the underlying capsis process has exited with exit code 0
        }

        [TestMethod]
        public void TestCapsisHandlerLifeCycleWithAsynchronousCallHappyPath()
        {
            CapsisProcessHandler handler = new(getCapsisPath(), getDataDirectory());
            handler.Start();

            // read the CSV data
            string data = File.ReadAllText("data/STR_RE2_70.csv");
            int[] fieldMatches = { 1, 2, 3, 4, 5, 6, 8, 10, 11, 12, 14, -1, 7, -1, -1, -1, -1, 13, -1 };
            handler.Simulate("Artemis", data, null, 2000, true, 100, "Stand", "NoChange", 2100, fieldMatches);
            Assert.AreEqual(false, handler.process.HasExited, "Process should not have exited yet");  // make sure the underlying capsis process is still alive
            Assert.AreEqual(true, handler.process.Responding, "Process should be responding");  // make sure the underlying capsis process is still responding            
            Assert.AreEqual(CapsisProcessHandler.State.READY, handler.getState());  // ensure handler is still in READY state (after an async call)
            double progress = handler.getProgress();
            while (!handler.isResultAvailable())
            {
                double newProgress = handler.getProgress();
                Assert.IsTrue(newProgress >= progress, "Progress should always increase only");
                progress = newProgress;
                Thread.Sleep(100);
            }

            Assert.IsTrue(handler.getResult().Length > 0);

            handler.Stop();
            Assert.AreEqual(true, handler.process.HasExited, "Process should have exited by now");  // make sure the underlying capsis process has exited
            Assert.AreEqual(0, handler.process.ExitCode, "Process exit code should be 0");  // make sure the underlying capsis process has exited with exit code 0
        }

        [TestMethod]
        public void TestCapsisHandlerLifeCycleWithAsynchronousCallHappyPath_RE2_120()
        {
            CapsisProcessHandler handler = new(getCapsisPath(), getDataDirectory());
            handler.Start();

            // read the CSV data
            string data = File.ReadAllText("data/STR_RE2_120.csv");
            int[] fieldMatches = { 1, 2, 3, 4, 5, 6, 8, 10, 11, 12, 14, -1, 7, -1, -1, -1, -1, 13, -1 };
            handler.Simulate("Artemis", data, null, 2000, true, 500, "Stand", "NoChange", 2080, fieldMatches);
            //Assert.AreEqual(false, handler.process.HasExited, "Process should not have exited yet");  // make sure the underlying capsis process is still alive
            //Assert.AreEqual(true, handler.process.Responding, "Process should be responding");  // make sure the underlying capsis process is still responding            
            Assert.AreEqual(CapsisProcessHandler.State.READY, handler.getState());  // ensure handler is still in READY state (after an async call)
            double progress = handler.getProgress();
            while (!handler.isResultAvailable())
            {
                double newProgress = handler.getProgress();
                Assert.IsTrue(newProgress >= progress, "Progress should always increase only");
                progress = newProgress;
                Thread.Sleep(100);
            }

            Assert.IsTrue(handler.getResult().Length > 0);

            handler.Stop();
            Assert.AreEqual(true, handler.process.HasExited, "Process should have exited by now");  // make sure the underlying capsis process has exited
            Assert.AreEqual(0, handler.process.ExitCode, "Process exit code should be 0");  // make sure the underlying capsis process has exited with exit code 0
        }

        [TestMethod]
        public void TestCapsisHandlerLifeCycleWithAsynchronousCallStopBeforeEnd()
        {
            CapsisProcessHandler handler = new(getCapsisPath(), getDataDirectory());
            handler.Start();

            // read the CSV data
            string data = File.ReadAllText("data/STR_RE2_70.csv");
            int[] fieldMatches = { 1, 2, 3, 4, 5, 6, 8, 10, 11, 12, 14, -1, 7, -1, -1, -1, -1, 13, -1 };
            handler.Simulate("Artemis", data, null, 2000, true, 1000, "Stand", "NoChange", 2100, fieldMatches);               
            Assert.AreEqual(false, handler.process.HasExited, "Process should not have exited yet");  // make sure the underlying capsis process is still alive
            Assert.AreEqual(true, handler.process.Responding, "Process should be responding");  // make sure the underlying capsis process is still responding            
            Assert.AreEqual(CapsisProcessHandler.State.READY, handler.getState());  // ensure handler is still in READY state (after an async call)
            handler.Stop();
            Assert.AreEqual(true, handler.process.HasExited, "Process should have exited by now");  // make sure the underlying capsis process has exited
            Assert.AreEqual(0, handler.process.ExitCode, "Process exit code should be 0");  // make sure the underlying capsis process has exited with exit code 0
        }

        [TestMethod]
        public void TestCapsisHandlerLifeCycleWithAsynchronousCallNoStopShouldExitAfterAWhile()
        {
            CapsisProcessHandler handler = new(getCapsisPath(), getDataDirectory());
            handler.Start();
            int processID = handler.process.Id;

            // read the CSV data
            string data = File.ReadAllText("data/STR_RE2_70.csv");
            int[] fieldMatches = { 1, 2, 3, 4, 5, 6, 8, 10, 11, 12, 14, -1, 7, -1, -1, -1, -1, 13, -1 };
            handler.Simulate("Artemis", data, null, 2000, true, 1000, "Stand", "NoChange", 2100, fieldMatches);

            Assert.IsTrue(IsProcessAlive(processID), "The process should exist right now");

            handler.process = null; // reinitializing the process member will force the monitoring thread to exit, and thus stop sending STATUS watchdog messages
            Thread.Sleep(12000);

            Assert.IsTrue(IsProcessAlive(processID), "The process should not exist anymore after a while");

            Assert.IsNull(handler.process);
        }

        [TestMethod]
        public void TestCapsisHandlerDefaultOutputRequest()
        {
            CapsisProcessHandler handler = new(getCapsisPath(), getDataDirectory());
            handler.Start();

            // read the CSV data
            string data = File.ReadAllText("data/STR_RE2_70.csv");
            int[] fieldMatches = { 1, 2, 3, 4, 5, 6, 8, 10, 11, 12, 14, -1, 7, -1, -1, -1, -1, 13, -1 };
            handler.Simulate("Artemis", data, null, 2000, true, 100, "Stand", "NoChange", 2100, fieldMatches);
            while (!handler.isResultAvailable())
            {
                Thread.Sleep(100);
            }

            CapsisProcessHandler.SimulationStatus status = handler.GetSimulationStatus();

            Assert.AreEqual(2, status.result.outputTypes.Count, "Default output request should lead to two outputs");
            Assert.IsTrue(status.result.outputTypes.Contains("AliveVolume_Broadleaved"), "Default output request should lead to AliveVolume_Broadleaved being output");
            Assert.IsTrue(status.result.outputTypes.Contains("AliveVolume_Coniferous"), "Default output request should lead to AliveVolume_Coniferous being output");

            handler.Stop();
        }
    }
}