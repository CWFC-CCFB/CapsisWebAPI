using System.Diagnostics;
using System.Threading;
using System.Xml;

namespace CapsisManager
{
    public class CapsisProcess
    {
        static readonly string ARTSCRIPT_PROTOCOL_OUT_STARTED = "<_STARTED_>:";
        static readonly string ARTSCRIPT_PROTOCOL_OUT_PROGRESS = "<_PROGRESS_>:";
        static readonly string ARTSCRIPT_PROTOCOL_OUT_COMPLETED = "<_COMPLETED_>";
        static readonly string ARTSCRIPT_PROTOCOL_OUT_ERROR = "<_ERROR_>:";

        public enum State
        {
            INIT,
            WAITING_FOR_START,
            STARTED,
            COMPLETED,
            ERROR,
            CANCELLED
        }

        string capsisPath;
        string inputFile;
        string outputFile;

        double progress;     // value between [0,1] only valid when in STARTED mode

        State state;
        string errorMessage;   
        Thread? thread;  // will stay null if used in sync mode
        Process? process;

        public CapsisProcess(string capsisPath, string inputFile, string outputFile)
        {            
            this.capsisPath = capsisPath;
            this.inputFile = inputFile;
            this.outputFile = outputFile;
            state = State.INIT;            
            thread = null;
            process = null;

        }

        public State getState() { return state; }
        public double getProgress() { return progress; }
        public string getErrorMessage() { return errorMessage; }    
        public void StartAsync()
        {
            if (state != State.INIT)
                throw new InvalidOperationException("CapsisProcess cannot start async thread in state " + Enum.GetName<State>(state));

            state = State.WAITING_FOR_START;    

            thread = new Thread(new ThreadStart(Start));

            thread.Start();

            while (thread.IsAlive && state == State.WAITING_FOR_START) 
            {
                Thread.Sleep(1);
            }

            if (!thread.IsAlive)
            {
                throw new InvalidOperationException("CapsisProcess could not start async thread");
            }
        }

        public void Start()
        {
            string classPathOption = "-cp ";
            classPathOption += capsisPath + ";";
            classPathOption += capsisPath + "ext/*;";
            classPathOption += capsisPath + "class;";

            ProcessStartInfo processStartInfo = new ProcessStartInfo();
            //processStartInfo.UseShellExecute = false;
            processStartInfo.RedirectStandardInput = true;
            processStartInfo.RedirectStandardOutput = true;
            processStartInfo.FileName = "java.exe";
            processStartInfo.Arguments = classPathOption + " artemis.script.ArtScript ";
            processStartInfo.Arguments += " --input \"" + inputFile + "\"";
            processStartInfo.Arguments += " --output \"" + outputFile + "\"";

            try
            {
                process = Process.Start(processStartInfo);
                if (process == null)
                {
                    errorMessage = "Could not start the process " + processStartInfo.FileName + " with arguments " + processStartInfo.Arguments;
                    state = State.ERROR;
                    if (thread == null) // only throw in sync mode
                        throw new Exception(errorMessage);
                    else
                        return;
                }
            }
            catch (Exception e)
            {
                errorMessage = "Exception caught while starting the process : " + e.Message;
                state = State.ERROR;
                if (thread == null) // only throw in sync mode
                    throw new Exception(errorMessage);
                else 
                    return;
            }

            var stdout = process.StandardOutput;
            bool stopReading = false;

            progress = 0.0;
            state = State.STARTED;            

            while (!process.HasExited && !stopReading)
            {                
                Task<string> readLineTask = stdout.ReadLineAsync();

                if (readLineTask.Wait(10000))   // timeout after 10s
                {
                    string line = readLineTask.Result;
                    if (line != null)
                    {
                        Console.WriteLine(line);
                        if (line.StartsWith(ARTSCRIPT_PROTOCOL_OUT_PROGRESS))
                        {
                            progress = Double.Parse(line.Substring(ARTSCRIPT_PROTOCOL_OUT_PROGRESS.Length));
                        }
                        else if (line.StartsWith(ARTSCRIPT_PROTOCOL_OUT_COMPLETED))
                        {
                            stopReading = true;
                            process.WaitForExit();
                            state = State.COMPLETED;
                        }
                        else if (line.StartsWith(ARTSCRIPT_PROTOCOL_OUT_ERROR))
                        {
                            stopReading = true;
                            process.WaitForExit();
                            errorMessage = line.Substring(ARTSCRIPT_PROTOCOL_OUT_ERROR.Length);
                            state = State.ERROR;
                            if (thread == null) 
                                throw new Exception(errorMessage);
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
                        if (thread == null) // only throw in sync mode
                            throw new Exception(errorMessage);
                        else
                            return;
                    }                    
                    state = State.ERROR;
                    stopReading = true;
                }
            }
        }

        public void Cancel() 
        { 
        }

        public bool isCompleted() { return state == State.COMPLETED; }
    }
}