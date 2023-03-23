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

        [TestMethod]
        public void TestCapsisHandlerStartNoStopShouldExitAfterAWhile()
        {
            try
            {
                CapsisProcessHandler handler = new(getCapsisPath(), getDataDirectory());
                handler.Start();
                int processID = handler.process.Id;

                try
                {
                    Process process = Process.GetProcessById(processID);                    
                }
                catch (Exception ex) 
                {
                    Assert.IsTrue(false, "The process should exist right now");
                }

                handler.process = null;
                Thread.Sleep(12000);

                try
                {
                    Process process = Process.GetProcessById(processID);
                    Assert.IsTrue(false, "The process should not exist anymore after a while");
                }
                catch (Exception ex) { }
            }
            catch (Exception ex)
            {
                Assert.IsTrue(false, ex.Message);
            }
        }

        [TestMethod]
        public void TestCapsisHandlerLifeCycleWithSynchronousCall()
        {
            try
            {
                CapsisProcessHandler handler = new(getCapsisPath(), getDataDirectory());
                handler.Start();
                List<string> variantList = handler.VariantList();
                Assert.IsFalse(variantList.Count == 0);     // make sure the variant list isn't empty
                Assert.AreEqual(false, handler.process.HasExited);  // make sure the underlying capsis process is still alive
                Assert.AreEqual(true, handler.process.Responding);  // make sure the underlying capsis process is still responding            
                Assert.AreEqual(CapsisProcessHandler.State.READY, handler.getState());  // ensure handler is still in READY state (after a synced call)
                handler.Stop();
                Assert.AreEqual(true, handler.process.HasExited);  // make sure the underlying capsis process has exited
                Assert.AreEqual(0, handler.process.ExitCode);  // make sure the underlying capsis process has exited with exit code 0
            }
            catch (Exception ex)
            {
                Assert.IsTrue(false, ex.Message);
            }
        }

        [TestMethod]
        public void TestCapsisHandlerLifeCycleWithAsynchronousCall()
        {
            try
            {
                CapsisProcessHandler handler = new(getCapsisPath(), getDataDirectory());
                handler.Start();

                // read the CSV data
                string data = File.ReadAllText("data/STR_RE2_70.csv");
                int[] fieldMatches = { 1, 2, 3, 4, 5, 6, 8, 10, 11, 12, 14, -1, 7, -1, -1, -1, -1, 13, -1 };
                handler.Simulate("Artemis", data, null, 2000, true, 1000, "Stand", "NoChange", 2100, fieldMatches);
                Thread.Sleep(2000);
                Assert.AreEqual(false, handler.process.HasExited);  // make sure the underlying capsis process is still alive
                Assert.AreEqual(true, handler.process.Responding);  // make sure the underlying capsis process is still responding            
                Assert.AreEqual(CapsisProcessHandler.State.READY, handler.getState());  // ensure handler is still in READY state (after an async call)
                handler.Stop();
                Assert.AreEqual(true, handler.process.HasExited);  // make sure the underlying capsis process has exited
                Assert.AreEqual(0, handler.process.ExitCode);  // make sure the underlying capsis process has exited with exit code 0
            }
            catch (Exception ex)
            {
                Assert.IsTrue(false, ex.Message);
            }
        }
    }
}