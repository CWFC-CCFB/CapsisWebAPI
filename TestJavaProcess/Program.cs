// See https://aka.ms/new-console-template for more information
using Capsis.Handler;
using Capsis.Handler.Main;
using Capsis.Handler.Requests;
using Newtonsoft.Json;
using System;
using System.Data;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Web;

//CapsisProcessHandler process = new CapsisProcessHandler("W:/NRCan/java/capsis", "W:/NRCan/java/ArtScriptTests");

try
{
    CapsisProcessHandler handler = new("W:/NRCan/java/capsis", "W:/NRCan/java/ArtScriptTests", false);
    handler.Start();

    // read the CSV data
    string data = File.ReadAllText("W:/NRCan/cs/CapsisWebAPI/Capsis.Handler.Tests/data/STR_RE2_70.csv");
    int[] fieldMatches = { 1, 2, 3, 4, 5, 6, 8, 10, 11, 12, 14, -1, 7, -1, -1, -1, -1, 13, -1 };
    handler.Simulate("Artemis", data, null, 2000, true, 100, "Stand", "NoChange", 2100, fieldMatches);
    //Assert.AreEqual(false, handler.process.HasExited);  // make sure the underlying capsis process is still alive
    //Assert.AreEqual(true, handler.process.Responding);  // make sure the underlying capsis process is still responding            
    //Assert.AreEqual(CapsisProcessHandler.State.READY, handler.getState());  // ensure handler is still in READY state (after an async call)
    double progress = handler.getProgress();
    while (!handler.isResultAvailable())
    {
        double newProgress = handler.getProgress();
        //Assert.IsTrue(newProgress >= progress);
        Thread.Sleep(100);
    }

    //Assert.IsTrue(handler.getResult().Length > 0);

    handler.Stop();
    //Assert.AreEqual(true, handler.process.HasExited);  // make sure the underlying capsis process has exited
    //Assert.AreEqual(0, handler.process.ExitCode);  // make sure the underlying capsis process has exited with exit code 0
}
catch (Exception ex)
{
    //Assert.IsTrue(false, ex.Message);
}

//try
//{
//    process.Start();

//    List<string> variantSpecies = process.VariantSpecies("Artemis");

//    int[] matches = { 2, 3, 4, 5, 6, 7, 9, 11, 12, 13, 15, -1, 8, -1, -1, -1, -1, 14, -1 };

//    // read the CSV data
//    string csvData = File.ReadAllText("W:/NRCan/java/ArtScriptTests/STR_R_EN90_50_9.csv");

//    List<OutputRequest> outputRequestList = new List<OutputRequest>();
//    Dictionary<string, List<string>> aggregationPatterns = new Dictionary<string, List<string>>();
//    aggregationPatterns["Coniferous"] = new List<string>();
//    aggregationPatterns["Coniferous"].Add("FD");
//    //aggregationPatterns["Broadleaved"] = new List<string>();
//    //aggregationPatterns["Broadleaved"].Add("EP");
//    outputRequestList.Add(new OutputRequest(RequestType.AliveVolume, aggregationPatterns));

//    process.Simulate("Artemis", csvData, outputRequestList, 2000, true, 100, "Stand", "NoChange", 2130, matches);

//    Thread.Sleep(5000);

//    bool res = process.Cancel();

//    CapsisProcessHandler.SimulationStatus status;
//    do
//    {
//        status = process.GetSimulationStatus();

//        if (status.status.Equals(CapsisProcessHandler.SimulationStatus.ERROR))
//        {
//            System.Console.WriteLine("Error was received by the Capsis process : " + status.errorMessage);
//            return;
//        }
//        else if (status.status.Equals(CapsisProcessHandler.SimulationStatus.CANCELLED))
//        {
//            System.Console.WriteLine("Task was cancelled");
//            return;
//        }    

//        Thread.Sleep(100);

//        Console.WriteLine("Progress : " + process.getProgress() * 100.0 + "%");
//    }
//    while (!status.status.Equals(CapsisProcessHandler.SimulationStatus.COMPLETED));

//    ScriptResult? simResult = status.result;

//    process.Stop();

//    Console.WriteLine("Process completed");

//}
//catch (Exception ex)
//{
//    //System.Console.WriteLine("Exception received while executing Capsis process : " + ex.ToString());    
//    int u = 0;
//}