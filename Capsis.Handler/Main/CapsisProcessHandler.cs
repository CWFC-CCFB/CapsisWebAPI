﻿/*
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

        //        private readonly String capsisPath;
        //        private readonly String dataDirectory;
        //        private readonly int _timeoutMilliSec;
        private readonly CapsisProcessHandlerSettings _settings;
        private bool disableJavaWatchdog;

        double progress;     // value between [0,1] only valid when in STARTED mode

        string? result;      // a string containing the result from the last COMPLETED message

        State state;
        string? errorMessage;   
        internal Thread? thread;  
        internal Process? process;
        bool ownsProcess;

        string? csvFilename;

        int bindToPort; // if this value is non-zero, then connect directly to this port instead of waiting for the port number to be advertised.  Typically used for debugging.

        StreamWriter? writerProcessInput;
        StreamReader? processStdOut = null;

        private readonly ILogger _logger;

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="capsisPath"> path to CAPSIS application </param> 
        /// <param name="dataDirectory"> path to data directory </param>
        /// <param name="logger"> a logger instance </param>
        /// <param name "timeoutMilliSec"> the number of millisecond before calling the JVM unresponsive </param>
        /// <param name="disableJavaWatchdog"> a boolean to enable or disable JavaWatchDog (by default to false) </param> 
        /// <param name="bindToPort"> an optional port number (typically in debug mode) </param> 
        public CapsisProcessHandler(CapsisProcessHandlerSettings settings,
//            String capsisPath, 
//            String dataDirectory, 
            ILogger logger,  
//            int timeoutMilliSec,
            bool disableJavaWatchdog = false, 
            int bindToPort = 0)
        {
            //            this.capsisPath = capsisPath;
            //            this.dataDirectory = dataDirectory;
            if (settings == null || logger == null)
                throw new ArgumentNullException("The settings and logger parameters cannot be non null!");
            this._settings = settings;
            this._logger = logger;
//            if (timeoutMilliSec < 0)
//                throw new ArgumentException("The timeoutMilliSec parameter should be positive (e.g. >= 0)!");
//            this._timeoutMilliSec = timeoutMilliSec;
            state = State.INIT;            
            thread = null;
            process = null;
            ownsProcess = false;
            result = null;
            writerProcessInput = null;
            this.disableJavaWatchdog = disableJavaWatchdog;
            this.bindToPort = bindToPort;
            csvFilename = null;
        }        

        public State GetState() { return state; }
        public double GetProgress() { return progress; }
        public string? GetErrorMessage() { return errorMessage; }    
        public string? GetResult() {
            lock (this)
            {
                return result;
            }
        }

        public string? GetCSVFilename() { return csvFilename; }

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
                throw new InvalidOperationException("CapsisProcess could not start async thread. Message : " + errorMessage);
            }
        }

        
        void SendMessage(ArtScriptMessage msg)
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
            progress = 0.0;

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
                processStartInfo.Arguments = classPathOption + " artemis.script.ArtScript";
                if (disableJavaWatchdog)
                    processStartInfo.Arguments += " --disableWatchdog";

                try
                {
                    _logger.LogInformation("Starting Java with parameters: " + processStartInfo.ToString());
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
                catch (Exception e)
                {
                    errorMessage = "Unable to bind to forced port " + bindToPort;
                    state = State.ERROR;
                    return;
                }
            }            

            while (readerProcessOutput != null && !stopListening)
            {                
                Task<string?> readLineTask = readerProcessOutput.ReadLineAsync();

                if (readLineTask.Wait(_settings.TimeoutMilliseconds))   
                {
                    string? line = readLineTask.Result;
                    if (line != null)
                    {
                        Console.WriteLine(DateTime.Now.ToString() + " RECEIVED :" + line);

                        try
                        {
                            ArtScriptMessage? msg = JsonConvert.DeserializeObject<ArtScriptMessage>(line);
                            if (msg != null)
                            {
                                if (msg.message.Equals(Enum.GetName<ArtScriptMessage.ArtScriptMessageType>(ArtScriptMessage.ArtScriptMessageType.ARTSCRIPT_MESSAGE_PORT)))
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
                                        catch (Exception e) 
                                        {
                                            Console.WriteLine("Cannot establish connection with process on port " + msg.payload + ".  Aborting.");
                                            try
                                            {
                                                if (process != null)
                                                    process.Kill();
                                            }
                                            catch (Exception ex)
                                            {
                                                errorMessage = "Exception caught while trying to kill the process with pid " + process.Id;
                                                state = State.ERROR;
                                                return;
                                            }
                                            state = State.ERROR;
                                            stopListening = true;
                                        }
                                    }
                                }
                                else if (msg.message.Equals(Enum.GetName<ArtScriptMessage.ArtScriptMessageType>(ArtScriptMessage.ArtScriptMessageType.ARTSCRIPT_MESSAGE_STATUS)))
                                {
                                    lock (this)
                                    {                                        
                                        if (msg.payload != null)
                                        {
                                            progress = Double.Parse(msg.payload);   
                                        }
                                        
                                        ArtScriptMessage reply = ArtScriptMessage.CreateMessageStatus();
                                        SendMessage(reply);
                                    }
                                }
                                else if (msg.message.Equals(Enum.GetName<ArtScriptMessage.ArtScriptMessageType>(ArtScriptMessage.ArtScriptMessageType.ARTSCRIPT_MESSAGE_SIMULATION_STARTED)))
                                {
                                    lock (this)
                                    {
                                        state = State.READY;
                                    }
                                }
                                else if (msg.message.Equals(Enum.GetName<ArtScriptMessage.ArtScriptMessageType>(ArtScriptMessage.ArtScriptMessageType.ARTSCRIPT_MESSAGE_STOP)))
                                {
                                    lock (this)
                                    {
                                        processStdOut = null;
                                        stopListening = true;
                                        if (ownsProcess)
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
                    if (ownsProcess && process != null)
                    {
                        Console.WriteLine(DateTime.Now.ToString() + " Process " + process.Id + " is unresponsive.  Killing it.");
                        try
                        {
                            process.Kill();
                        }
                        catch (Exception ex)
                        {
                            errorMessage = "Exception caught while trying to kill the process with pid " + process.Id;
                            state = State.ERROR;
                            return;
                        }
                        processStdOut = null;
                        readerProcessOutput = null;
                        state = State.ERROR;
                        stopListening = true;
                    }
                }
            }

            if (ownsProcess && process != null && process.HasExited)  // at this point, the process should be running
            {
                errorMessage = "Process terminated unexpectedly in directory " + _settings.CapsisDirectory;
                state = State.ERROR;
                return;
            }
        }
        
        public void Stop()
        {
            if (ownsProcess && process == null)
                throw new Exception("Cannot send stop message on null process");

            lock (this)
            {
                state = State.OPERATION_PENDING;
                ArtScriptMessage msg = ArtScriptMessage.CreateMessageStop();
                SendMessage(msg);
            }

            while (state == State.OPERATION_PENDING || (process != null && !process.HasExited))
                Thread.Sleep(1);
        }

        public Guid Simulate(string variant, string data, List<OutputRequest>? outputRequestList, int initialDateYr, bool isStochastic, int nbRealizations, string applicationScale, string climateChange, int finalDateYr, int[]? fieldMatches)
        {
            if (ownsProcess && process == null)
                throw new Exception("Cannot send stop message on null process");

            Enum.Parse(typeof(Variant), variant);

            Guid guid = Guid.NewGuid();

            lock (this)
            {
                // save csv file to data directory
                csvFilename = _settings.DataDirectory + Path.AltDirectorySeparatorChar + guid.ToString() + ".csv";
                File.WriteAllText(csvFilename, data);

                state = State.OPERATION_PENDING;
                result = null;
                ArtScriptMessage msg = ArtScriptMessage.CreateMessageSimulate(initialDateYr, isStochastic, nbRealizations, applicationScale, climateChange, finalDateYr, fieldMatches, csvFilename);
                SendMessage(msg);
            }

            while (state == State.OPERATION_PENDING)
                Thread.Sleep(1);

            return guid;
        }

        public List<string> VariantList()
        {
            return new List<string> { "Artemis" };
        }

        public List<string> VariantSpecies(string variant, VariantSpecies.Type type = Capsis.Handler.Main.VariantSpecies.Type.All)
        {
            if (ownsProcess && process == null)
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
                if (state == State.READY)
                {
                    if (result != null)
                        return new SimulationStatus(SimulationStatus.COMPLETED, null, progress, result);
                    else
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