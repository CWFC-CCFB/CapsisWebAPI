using Capsis.Handler.Main;
using Capsis.Handler.Requests;
using Newtonsoft.Json;
using System;
using System.Data;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Xml;
using static System.Net.Mime.MediaTypeNames;

[assembly: InternalsVisibleTo("Capsis.Handler.Tests")]
namespace Capsis.Handler
{
    public class CapsisProcessHandler
    {
        public class SimulationStatus
        {
            public static readonly string COMPLETED = "COMPLETED";
            public static readonly string IN_PROGRESS = "IN_PROGRESS";
            public static readonly string ERROR = "ERROR";
            public static readonly string STOPPED = "STOPPED";

            public SimulationStatus(string status, string? errorMessage, double progress, string? result)
            {
                this.status = status;
                this.errorMessage = errorMessage;
                this.progress = progress;
                this.result = result == null ? null : JsonConvert.DeserializeObject<ScriptResult>(result);
            }

            public string status { get; set; }
            public string? errorMessage { get; set; }
            public double progress { get; set; }
            public ScriptResult? result { get; set; }            
        }

        public enum Variant { Artemis }

        public enum State
        {
            INIT,
            OPERATION_PENDING,
            READY,           
            STOPPED,
            ERROR            
        }

        string capsisPath;
        string dataDirectory;
        bool enableJavaWatchdog;

        double progress;     // value between [0,1] only valid when in STARTED mode

        string result;      // a string containing the result from the last COMPLETED message

        State state;
        string errorMessage;   
        internal Thread? thread;  // will stay null if used in sync mode
        internal Process? process;

        public CapsisProcessHandler(string capsisPath, string dataDirectory, bool enableJavaWatchdog = true)
        {            
            this.capsisPath = capsisPath;
            this.dataDirectory = dataDirectory;
            state = State.INIT;            
            thread = null;
            process = null;
            result = null;
            this.enableJavaWatchdog = enableJavaWatchdog;
        }        

        public State getState() { return state; }
        public double getProgress() { return progress; }
        public string getErrorMessage() { return errorMessage; }    
        public string getResult() {
            lock (this)
            {
                return result;
            }
        }

        public void Start()
        {
            if (state != State.INIT)
                throw new InvalidOperationException("CapsisProcess cannot start async thread in state " + Enum.GetName<State>(state));

            state = State.OPERATION_PENDING;    

            thread = new Thread(new ThreadStart(LaunchProcess));

            thread.Start();

            while (thread.IsAlive && state == State.OPERATION_PENDING) 
            {
                Thread.Sleep(1);
            }

            if (!thread.IsAlive || state != State.READY)
            {
                throw new InvalidOperationException("CapsisProcess could not start async thread.  Message : " + errorMessage);
            }
        }

        void SendMessage(ArtScriptMessage msg)
        {
            if (process == null)
                throw new InvalidOperationException("Cannot send message to a null process");

            process.StandardInput.WriteLine(JsonConvert.SerializeObject(msg));

            process.StandardInput.Flush();
        }

        void LaunchProcess()
        {
            string classPathOption = "-cp ";
            classPathOption += capsisPath + Path.AltDirectorySeparatorChar +  ";";
            classPathOption += capsisPath + Path.AltDirectorySeparatorChar + "ext/*;";
            classPathOption += capsisPath + Path.AltDirectorySeparatorChar  + "class;";

            ProcessStartInfo processStartInfo = new ProcessStartInfo();
            processStartInfo.UseShellExecute = false;
            processStartInfo.RedirectStandardInput = true;
            processStartInfo.RedirectStandardOutput = true;            
            processStartInfo.FileName = "java.exe";
            processStartInfo.Arguments = classPathOption + " artemis.script.ArtScript";
            if (!enableJavaWatchdog)
                processStartInfo.Arguments += " --disableWatchdog";

            try
            {
                process = Process.Start(processStartInfo);
                if (process == null)
                {
                    errorMessage = "Could not start the process " + processStartInfo.FileName + " with arguments " + processStartInfo.Arguments;
                    state = State.ERROR;                                        
                    return;
                }
            }
            catch (Exception e)
            {
                errorMessage = "Exception caught while starting the process : " + e.Message;
                state = State.ERROR;
                return;
            }

            bool stopListening = false;

            progress = 0.0;            

            while (process != null && !process.HasExited && !stopListening)
            {                
                Task<string> readLineTask = process.StandardOutput.ReadLineAsync();

                if (readLineTask.Wait(10000))   // timeout after 10s
                {
                    string line = readLineTask.Result;
                    if (line != null)
                    {
                        Console.WriteLine("RECEIVED :" + line);

                        try
                        {
                            ArtScriptMessage msg = JsonConvert.DeserializeObject<ArtScriptMessage>(line);
                            if (msg != null)
                            {
                                if (msg.message.Equals(Enum.GetName<ArtScriptMessage.ArtScriptMessageType>(ArtScriptMessage.ArtScriptMessageType.ARTSCRIPT_MESSAGE_STATUS)))
                                {
                                    lock (this)
                                    {
                                        state = State.READY;

                                        if (msg.payload != null)
                                        {
                                            progress = Double.Parse(msg.payload);   
                                        }
                                        
                                        ArtScriptMessage reply = ArtScriptMessage.CreateMessageStatus();
                                        SendMessage(reply);
                                    }
                                }
                                else if (msg.message.Equals(Enum.GetName<ArtScriptMessage.ArtScriptMessageType>(ArtScriptMessage.ArtScriptMessageType.ARTSCRIPT_MESSAGE_STOP)))
                                {
                                    lock (this)
                                    {
                                        stopListening = true;
                                        process.WaitForExit();
                                        state = State.READY;
                                    }
                                }
                                else if (msg.message.Equals(Enum.GetName<ArtScriptMessage.ArtScriptMessageType>(ArtScriptMessage.ArtScriptMessageType.ARTSCRIPT_MESSAGE_ERROR)))
                                {
                                    lock (this)
                                    {                                                                                
                                        state = State.ERROR;
                                        errorMessage = msg.payload;
                                    }
                                }
                                else if (msg.message.Equals(Enum.GetName<ArtScriptMessage.ArtScriptMessageType>(ArtScriptMessage.ArtScriptMessageType.ARTSCRIPT_MESSAGE_COMPLETED)))
                                {
                                    lock (this)
                                    {                                        
                                        state = State.READY;
                                        result = msg.payload;
                                    }
                                }
                            }
                        }
                        catch (JsonReaderException e)
                        {
                            // silently ignore this unknown message
                        }
                    }
                }
                else
                {   // process is non-responsive
                    Console.WriteLine("Process " + process.Id + " is unresponsive.  Killing it.");
                    try { 
                        process.Kill(); 
                    } catch (Exception ex) {
                        errorMessage = "Exception caught while trying to kill the process with pid " + process.Id;
                        state = State.ERROR;
                        return;
                    }                    
                    state = State.ERROR;
                    stopListening = true;
                }
            }

            if (process != null && process.HasExited)  // at this point, the process should be running
            {
                errorMessage = "Process terminated unexpectedly in directory " + capsisPath;
                state = State.ERROR;
                return;
            }
        }
        
        public void Stop()
        {
            if (process == null)
                throw new Exception("Cannot send stop message on null process");

            lock (this)
            {
                state = State.OPERATION_PENDING;
                ArtScriptMessage msg = ArtScriptMessage.CreateMessageStop();
                SendMessage(msg);
            }

            while (state == State.OPERATION_PENDING)
                Thread.Sleep(1);
        }

        public void Simulate(string variant, string data, List<OutputRequest>? outputRequestList, int initialDateYr, bool isStochastic, int nbRealizations, string applicationScale, string climateChange, int finalDateYr, int[]? fieldMatches)
        {
            if (process == null)
                throw new Exception("Cannot send stop message on null process");

            Enum.Parse(typeof(Variant), variant);

            lock (this)
            {
                // save csv file to data directory
                string filename = dataDirectory + Path.AltDirectorySeparatorChar + process.Id.ToString() + ".csv";
                File.WriteAllText(filename, data);

                state = State.OPERATION_PENDING;
                result = null;
                ArtScriptMessage msg = ArtScriptMessage.CreateMessageSimulate(initialDateYr, isStochastic, nbRealizations, applicationScale, climateChange, finalDateYr, fieldMatches, filename);
                SendMessage(msg);
            }

            while (state == State.OPERATION_PENDING)
                Thread.Sleep(1);
        }

        public List<string> VariantList()
        {
            return new List<string> { "Artemis" };
        }

        public List<string> VariantSpecies(string variant, VariantSpecies.Type type = Capsis.Handler.Main.VariantSpecies.Type.All)
        {
            if (process == null)
                throw new Exception("Cannot send stop message on null process");

            lock (this)
            {
                state = State.OPERATION_PENDING;
                result = null;
                ArtScriptMessage msg = ArtScriptMessage.CreateMessageGetSpeciesOfType(type);
                SendMessage(msg);
            }

            while (state == State.OPERATION_PENDING)
                Thread.Sleep(1);

            lock (this)
            {
                return JsonConvert.DeserializeObject<List<string>>(result);
            }
        }

        public List<RequestType> OutputRequestTypes()
        {               
            return RequestType.requestTypeList;
        }

        public List<ImportFieldElementIDCard> VariantFieldList(string variant)
        {
            Enum.Parse(typeof(Variant), variant);

            lock (this)
            {
                state = State.OPERATION_PENDING;
                result = null;
                ArtScriptMessage msg = ArtScriptMessage.CreateMessageGetFieldList();
                SendMessage(msg);
            }

            while (state == State.OPERATION_PENDING)
                Thread.Sleep(1);

            lock (this)
            {
                return JsonConvert.DeserializeObject<List<ImportFieldElementIDCard>>(result);
            }
        }

        public bool isReady()
        {
            lock(this)
            {
                return state == State.READY;
            }
        }

        public bool isResultAvailable() { return result != null; }

        public SimulationStatus GetSimulationStatus()
        {
            lock (this)
            {
                if (state == State.READY && result != null)
                {
                    return new SimulationStatus(SimulationStatus.COMPLETED, null, progress, result);
                }
                else if (state == State.OPERATION_PENDING)
                {
                    return new SimulationStatus(SimulationStatus.IN_PROGRESS, null, progress, null);
                }
                else if (state == State.STOPPED)
                {
                    return new SimulationStatus(SimulationStatus.STOPPED, null, 0.0, null);
                }
                else if (state == State.ERROR)
                {
                    return new SimulationStatus(SimulationStatus.ERROR, errorMessage, 0.0, null);
                }
                else
                {
                    return new SimulationStatus(SimulationStatus.ERROR, "Unknown simulation status", 0.0, null);
                }
            }
        }        
    }
}