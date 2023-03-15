// See https://aka.ms/new-console-template for more information
using CapsisManager;
using System;
using System.Diagnostics;

CapsisProcess process = new CapsisProcess("W:/NRCan/java/capsis/", "W:/NRCan/java/ArtScriptTests/input.json", "W:/NRCan/java/ArtScriptTests/output.json");

try
{
    process.StartAsync();

    while (!process.isCompleted())
    {
        if (process.getState() == CapsisProcess.State.ERROR)
        {
            System.Console.WriteLine("Error was received by the Capsis process : " + process.getErrorMessage());
            return;
        }

        Thread.Sleep(100);
        
        Console.WriteLine("Progress : " + process.getProgress() * 100.0 + "%");
    }

    Console.WriteLine("Process completed");

}
catch (Exception ex)
{
    //System.Console.WriteLine("Exception received while executing Capsis process : " + ex.ToString());    
    int u = 0;
}