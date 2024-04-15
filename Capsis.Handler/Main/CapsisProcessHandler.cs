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
using Capsis.Handler.Requests;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System.Diagnostics;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using static Capsis.Handler.CapsisWebAPIMessage;

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
                Status = status;
                ErrorMessage = errorMessage;
                Progress = progress;
                Result = result == null ? null : JsonConvert.DeserializeObject<ScriptResult>(result);
            }

            public string Status { get; private set; }
            public string? ErrorMessage { get; private set; }
            public double Progress { get; private set; }
            public ScriptResult? Result { get; private set; }            
        }

        public enum Variant { ARTEMIS, ARTEMIS2014 }

        public enum State
        {
            INIT,
            OPERATION_PENDING,
            READY,           
            STOPPED,
            ERROR            
        }

        //        private readonly String capsisPath;
        //        private readonly String dataDirectory;
        //        private readonly int _timeoutMilliSec;
        private readonly CapsisProcessHandlerSettings _settings;
        private bool disableJavaWatchdog;

        public double Progress { get; private set; }     // value between [0,1] only valid when in STARTED mode

        private string? Result { get; set; }      // a string containing the result from the last COMPLETED message

        public State Status { get; private set; }


        public string? ErrorMessage { get; private set; }  
        internal Thread? thread;  
        internal Process? process;
        bool ownsProcess;

        string? csvFilename;

        int bindToPort; // if this value is non-zero, then connect directly to this port instead of waiting for the port number to be advertised.  Typically used for debugging.

        StreamWriter? writerProcessInput;
        StreamReader? processStdOut = null;

        private readonly ILogger _logger;
        private bool stopRequested;

        private readonly Variant _variant;
        private readonly int _timeoutMillisec;

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="capsisPath"> path to CAPSIS application </param> 
        /// <param name="dataDirectory"> path to data directory </param>
        /// <param name="logger"> a logger instance </param>
        /// <param name "timeoutMilliSec"> the number of millisecond before calling the JVM unresponsive </param>
        /// <param name="disableJavaWatchdog"> a boolean to enable or disable JavaWatchDog (by default to false) </param> 
        /// <param name="bindToPort"> an optional port number (typically in debug mode) </param> 
        /// <param name="refHandler"> a boolean to indicate whether this handler is the reference handler</param>
        public CapsisProcessHandler(CapsisProcessHandlerSettings settings,
            ILogger logger,
            Variant variant,
            bool disableJavaWatchdog = false, 
            int bindToPort = 0,
            bool refHandler = false)
        {
            if (settings == null || logger == null)
                throw new ArgumentNullException("The settings and logger parameters cannot be non null!");
            this._settings = settings;
            this._logger = logger;
            this._variant = variant;
            Status = State.INIT;            
            thread = null;
            process = null;
            ownsProcess = false;
            Result = null;
            writerProcessInput = null;
            this.disableJavaWatchdog = disableJavaWatchdog;
            this.bindToPort = bindToPort;
            csvFilename = null;
            stopRequested = false;
            this._timeoutMillisec = refHandler ? settings.TimeoutMillisecondsRefHandler : settings.TimeoutMilliseconds;
        }        

        public string? GetResult() {
            lock (this)
            {
                return Result;
            }
        }

        public string? GetCSVFilename() { return csvFilename; }

        public void Start()
        {
            if (Status != State.INIT)
                throw new InvalidOperationException("CapsisProcess cannot start async thread in state " + Enum.GetName<State>(Status));

            Status = State.OPERATION_PENDING;    

            thread = new Thread(new ThreadStart(LaunchProcess));

            thread.Start();

            while (thread.IsAlive && Status == State.OPERATION_PENDING) 
            {
                Thread.Sleep(1);
            }

            if (!thread.IsAlive || Status != State.READY)
            {
                throw new InvalidOperationException("CapsisProcess could not start async thread. Message : " + ErrorMessage);
            }
        }

        
        void SendMessage(CapsisWebAPIMessage msg)
        {
            if (writerProcessInput == null)
                throw new InvalidOperationException("Cannot send message to a null process");

            lock (writerProcessInput)
            {
                string jsonMSG = JsonConvert.SerializeObject(msg);
                writerProcessInput.WriteLine(jsonMSG);

                writerProcessInput.Flush();

                Console.WriteLine("Sending message : " + jsonMSG);
            }
        }

        void StdOutPrinter()
        {
            Console.WriteLine(DateTime.Now.ToString() + " StdOutPrinter starting");

            while (processStdOut != null)
            {
                Console.WriteLine("_JAVA : " + DateTime.Now.ToString() + " :" + processStdOut.ReadLine());
            }

            Console.WriteLine(DateTime.Now.ToString() + " StdOutPrinter stopping");
        }

        /// <summary>
        /// Add quotes to the string if it contains at least one space. This method 
        /// is needed to properly initialize the Java Virtual Machine. If some paths
        /// contain spaces without being quoted, the initialization systematically fails.
        /// </summary>
        /// <param name="str"></param>
        /// <returns></returns>
        private string AddQuoteToStringIfNeeded(string str)
        {   
            string trimmedStr = str.Trim();

            return trimmedStr.Contains(" ") ?  "\"" + trimmedStr + "\"" : trimmedStr;
        }

  

        void LaunchProcess()
        {
            bool stopListening = false;
            Progress = 0.0;

            StreamReader readerProcessOutput;
            TcpClient? client = null;

            if (bindToPort == 0)    // if a port is specified, then do not spawn a new process, but connect to it
            {
                String classPathOption = "-cp ";
                classPathOption += AddQuoteToStringIfNeeded(_settings.CapsisDirectory + Path.AltDirectorySeparatorChar + "class") + ";";
                classPathOption += AddQuoteToStringIfNeeded(_settings.CapsisDirectory + Path.AltDirectorySeparatorChar + "ext/*");

                ProcessStartInfo processStartInfo = new();
                processStartInfo.UseShellExecute = false;
                processStartInfo.RedirectStandardInput = true;
                processStartInfo.RedirectStandardOutput = true;
                processStartInfo.FileName = "java.exe";
                processStartInfo.Arguments = classPathOption + " capsis.util.extendeddefaulttype.webapi.CapsisWebAPIScriptRunner --variant " + _variant.ToString();
                if (disableJavaWatchdog)
                    processStartInfo.Arguments += " --disableWatchdog";

                try
                {
                    _logger.LogInformation("Starting Java with parameters: " + processStartInfo.Arguments);
                    process = Process.Start(processStartInfo);
                    if (process == null)
                    {
                        ErrorMessage = "Could not start the process " + processStartInfo.FileName + " with arguments " + processStartInfo.Arguments;
                        Status = State.ERROR;
                        return;
                    }
                }
                catch (Exception e)
                {
                    ErrorMessage = "Exception caught while starting the process : " + e.Message;
                    Status = State.ERROR;
                    return;
                }

                ownsProcess = true;

                readerProcessOutput = process.StandardOutput;
            }                                    
            else            
            {
                try
                {
                    client = new TcpClient("localhost", bindToPort);

                    // do not change the state, let's switch to the new stream and wait for a status message to do so
                    readerProcessOutput = new StreamReader(client.GetStream());
                    writerProcessInput = new StreamWriter(client.GetStream());                    
                }
                catch (Exception)
                {
                    ErrorMessage = "Unable to bind to forced port " + bindToPort;
                    Status = State.ERROR;
                    return;
                }
            }            

            while (readerProcessOutput != null && !stopListening)
            {                
                Task<string?> readLineTask = readerProcessOutput.ReadLineAsync();

                if (readLineTask.Wait(this._timeoutMillisec))   // We give plenty of time for the reference handler
                {
                    string? line = readLineTask.Result;
                    if (line != null)
                    {
                        Console.WriteLine(DateTime.Now.ToString() + " RECEIVED :" + line);

                        try
                        {
                            CapsisWebAPIMessage? msg = JsonConvert.DeserializeObject<CapsisWebAPIMessage>(line);
                            if (msg != null)
                            {
                                if (msg.message.Equals(Enum.GetName<CapsisWebAPIMessageType>(CapsisWebAPIMessageType.MESSAGE_PORT)))
                                {   // this is a special message : it tells the current process to switch the communication to port x
                                    lock (this)
                                    {
                                        try
                                        {
                                            int port = Int32.Parse(msg.payload);

                                            client = new TcpClient("localhost", port);

                                            // do not change the state, let's switch to the new stream and wait for a status message to do so
                                            processStdOut = readerProcessOutput;
                                            readerProcessOutput = new StreamReader(client.GetStream());
                                            writerProcessInput = new StreamWriter(client.GetStream());
                                            Thread stdoutWriter = new(new ThreadStart(StdOutPrinter));
                                            stdoutWriter.Start();
                                        }
                                        catch (Exception) 
                                        {
                                            Console.WriteLine("Cannot establish connection with process on port " + msg.payload + ".  Aborting.");
                                            try
                                            {
                                                if (process != null)
                                                    process.Kill();
                                            }
                                            catch (Exception)
                                            {
                                                ErrorMessage = "Exception caught while trying to kill the process with pid " + (process != null ? process.Id : "unknown");
                                                Status = State.ERROR;
                                                return;
                                            }
                                            Status = State.ERROR;
                                            stopListening = true;
                                        }
                                    }
                                }
                                else if (msg.message.Equals(Enum.GetName<CapsisWebAPIMessageType>(CapsisWebAPIMessageType.MESSAGE_STATUS)))
                                {
                                    lock (this)
                                    {                                        
                                        if (msg.payload != null)
                                        {
                                            Progress = Double.Parse(msg.payload);   
                                        }

                                        CapsisWebAPIMessage reply = CapsisWebAPIMessage.CreateMessageStatus();
                                        SendMessage(reply);
                                    }
                                }
                                else if (msg.message.Equals(Enum.GetName<CapsisWebAPIMessageType>(CapsisWebAPIMessageType.MESSAGE_SIMULATION_STARTED)))
                                {
                                    lock (this)
                                    {
                                        Status = State.READY;
                                    }
                                }
                                else if (msg.message.Equals(Enum.GetName<CapsisWebAPIMessageType>(CapsisWebAPIMessageType.MESSAGE_STOP)))
                                {
                                    lock (this)
                                    {
                                        processStdOut = null;
                                        stopListening = true;
                                        if (ownsProcess)
                                            process.WaitForExit();
                                        Status = State.READY;
                                    }
                                }
                                else if (msg.message.Equals(Enum.GetName<CapsisWebAPIMessageType>(CapsisWebAPIMessageType.MESSAGE_ERROR)))
                                {
                                    lock (this)
                                    {                                                                                
                                        Status = State.ERROR;
                                        ErrorMessage = msg.payload;
                                    }
                                }
                                else if (msg.message.Equals(Enum.GetName<CapsisWebAPIMessageType>(CapsisWebAPIMessageType.MESSAGE_COMPLETED)))
                                {
                                    lock (this)
                                    {                                        
                                        Status = State.READY;
                                        Result = msg.payload;
                                    }
                                }
                            }
                        }
                        catch (JsonReaderException)
                        {
                            // silently ignore this unknown message
                        }
                    }
                }
                else
                {   // process is non-responsive
                    if (ownsProcess && process != null)
                    {
                        try
                        {
                            ErrorMessage = DateTime.Now.ToString() + " Process " + process.Id + " is unresponsive. Killing it.";
                            Status = State.ERROR;
                            process.Kill();
                        }
                        catch (Exception)
                        {
                            ErrorMessage = "Exception caught while trying to kill the process with pid " + process.Id;
                            Status = State.ERROR;
                            return;
                        }
                        processStdOut = null;
                        readerProcessOutput = null;
                        stopListening = true;
                    }
                }
            }

            if (ownsProcess && process != null && process.HasExited && !stopRequested)  // at this point, the process should be running
            {
                ErrorMessage = "Process terminated unexpectedly in directory " + _settings.CapsisDirectory;
                Status = State.ERROR;
                return;
            }
        }
        
        public void Stop()
        {
            stopRequested = true;
            if (ownsProcess && process == null)
                throw new InvalidOperationException("Cannot send stop message on null process");

            lock (this)
            {
                Status = State.OPERATION_PENDING;
                CapsisWebAPIMessage msg = CapsisWebAPIMessage.CreateMessageStop();
                SendMessage(msg);
            }

            while (Status == State.OPERATION_PENDING || (process != null && !process.HasExited))
                Thread.Sleep(1);
        }

        public Guid Simulate(string data, List<OutputRequest>? outputRequestList, int initialDateYr, bool isStochastic, int nbRealizations, string applicationScale, string climateChange, int finalDateYr, int[]? fieldMatches)
        {
            if (ownsProcess && process == null)
                throw new Exception("Cannot send stop message on null process");

            Guid guid = Guid.NewGuid();

            lock (this)
            {
                // save csv file to data directory
                csvFilename = _settings.DataDirectory + Path.AltDirectorySeparatorChar + guid.ToString() + ".csv";
                File.WriteAllText(csvFilename, data);

                Status = State.OPERATION_PENDING;
                Result = null;
                CapsisWebAPIMessage msg = CapsisWebAPIMessage.CreateMessageSimulate(outputRequestList, initialDateYr, isStochastic, nbRealizations, applicationScale, climateChange, finalDateYr, fieldMatches, csvFilename);
                SendMessage(msg);
            }

            while (Status == State.OPERATION_PENDING)
                Thread.Sleep(1);

            return guid;
        }

        public static List<Variant> GetVariantList()
        {
            return new List<Variant> { Variant.ARTEMIS, Variant.ARTEMIS2014 };
        }

        public List<string>? VariantSpecies(VariantSpecies.Type type = Capsis.Handler.Main.VariantSpecies.Type.All)
        {
            if (ownsProcess && process == null)
                throw new Exception("Cannot send stop message on null process");

            lock (this)
            {
                Status = State.OPERATION_PENDING;
                Result = null;
                CapsisWebAPIMessage msg = CapsisWebAPIMessage.CreateMessageGetSpeciesOfType(type);
                SendMessage(msg);
            }

            while (Status == State.OPERATION_PENDING)
                Thread.Sleep(1);

            lock (this)
            {
                return JsonConvert.DeserializeObject<List<string>>(Result);
            }
        }

        public List<string> VariantRequests()
        {
            lock (this)
            {
                Status = State.OPERATION_PENDING;
                Result = null;
                CapsisWebAPIMessage msg = CapsisWebAPIMessage.CreateMessageGetRequestList();
                SendMessage(msg);
            }

            while (Status == State.OPERATION_PENDING)
                Thread.Sleep(1);

            lock (this)
            {
                return JsonConvert.DeserializeObject<List<string>>(Result);
            }
        }

        public List<ImportFieldElementIDCard> VariantFieldList()
        {
            lock (this)
            {
                Status = State.OPERATION_PENDING;
                Result = null;
                CapsisWebAPIMessage msg = CapsisWebAPIMessage.CreateMessageGetFieldList();
                SendMessage(msg);
            }

            while (Status == State.OPERATION_PENDING)
                Thread.Sleep(1);

            lock (this)
            {
                return JsonConvert.DeserializeObject<List<ImportFieldElementIDCard>>(Result);
            }
        }

        public bool isReady()
        {
            lock(this)
            {
                return Status == State.READY;
            }
        }

        public bool isResultAvailable() { return Result != null; }

        public SimulationStatus GetSimulationStatus()
        {
            lock (this)
            {
                if (Status == State.READY)
                {
                    return new SimulationStatus(Result != null ? SimulationStatus.COMPLETED : SimulationStatus.IN_PROGRESS,
                        null,
                        Progress,
                        Result);
                }
                else if (Status == State.STOPPED)
                {
                    return new SimulationStatus(SimulationStatus.STOPPED, null, 0.0, null);
                }
                else if (Status == State.ERROR)
                {
                    return new SimulationStatus(SimulationStatus.ERROR, ErrorMessage, 0.0, null);
                }
                else
                {
                    return new SimulationStatus(SimulationStatus.ERROR, "Unknown simulation status", 0.0, null);
                }
            }
        }        
    }
}