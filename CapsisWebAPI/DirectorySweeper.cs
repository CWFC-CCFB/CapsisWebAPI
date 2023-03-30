namespace CapsisWebAPI
{
    /// <summary>
    /// A class that implements a thread that continuously monitors the specified folder and will delete any file that hasn't been accessed for more than n minutes.
    /// It is used to prevent lingering files in the data folder that could be left over due to malfunctioning error handling
    /// </summary>
    public class DirectorySweeper
    {
        internal Thread? thread;
        private EventWaitHandle ewhStop;

        string directoryPath;
        int deleteIfOlderThanMins;

        bool stopThread;

        public DirectorySweeper(string directoryPath, int deleteIfOlderThanMins)
        {
            this.directoryPath = directoryPath;
            this.deleteIfOlderThanMins = deleteIfOlderThanMins;

            ewhStop = new EventWaitHandle(false, EventResetMode.AutoReset);
            thread = new Thread(new ThreadStart(ThreadLoop));
            thread.Start();
        }

        void Stop() 
        {
            ewhStop.Set();
            if (thread != null && thread.IsAlive)
                thread.Join();
        }

        void ThreadLoop()
        {
            bool stopThread = false;

            while (!stopThread)
            {
                bool waitResult = ewhStop.WaitOne(10000);   // wait for 10 sec for the stop event

                if (waitResult)
                {
                    stopThread = true;
                }
                else
                {
                    string[] fileList = Directory.GetFiles(directoryPath);
                    DateTime now = DateTime.Now;

                    foreach (string file in fileList)
                    {
                        DateTime lastAccessTime = Directory.GetLastAccessTime(file);
                        TimeSpan span = now.Subtract(lastAccessTime);
                        if (span.TotalMinutes > deleteIfOlderThanMins)
                        {
                            File.Delete(file);
                        }
                    }
                }
            }
        }

    }
}