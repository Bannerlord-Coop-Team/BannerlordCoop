using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;


namespace BannerlordSystemTestingLibrary
{
    class GameProcess
    {
        string GamePath = Settings.Default.GamePath;
        Process process;
        readonly ProcessStartInfo pInfo = new ProcessStartInfo();

        public bool Running { get; private set; }

        [DllImport("User32.dll")]
        static extern int SetForegroundWindow(IntPtr point);
        public GameProcess(string args)
        {
            if (string.IsNullOrEmpty(GamePath))
            {
                FindGamePath();
            }

            pInfo.FileName = GamePath + @"\Bannerlord.exe";
            pInfo.WorkingDirectory = GamePath;

            pInfo.Arguments = args;
        }

        public GameProcess(Process p)
        {
            process = p;
            Running = !process.HasExited;
        }

        public void Start()
        {
            Running = true;
            process = Process.Start(pInfo);
            process.Exited += (sender, e) => { Running = false; };

            if (process.MainWindowTitle == "Safe Mode")
            {
                SetForegroundWindow(process.MainWindowHandle);
                SendKeys.SendWait("{TAB}");
                SendKeys.SendWait("{ENTER}");
            }

            Trace.WriteLine(process.MainWindowTitle);
        }

        public int GetPID()
        {
            return process.Id;
        }

        public void Close()
        {
            process.CloseMainWindow();
        }

        public void Kill()
        {
            process.Kill();
        }

        #region private

        private void FindGamePath()
        {
            DriveInfo[] allDrives = DriveInfo.GetDrives();

            List<Task<string>> searchTasks = new List<Task<string>>();

            CancellationTokenSource cts = new CancellationTokenSource();

            foreach (DriveInfo drive in allDrives)
            {
                Task<string> t = new Task<string>(() =>
                {
                    Console.WriteLine($"Starting search on drive {drive.Name}");
                    RecursiveFileProcessor fp = new RecursiveFileProcessor("Bannerlord.exe");
                    string result = fp.ProcessDirectory(drive.Name, cts.Token);

                    if (result != null)
                    {
                        return result;
                    }
                    return null;
                });

                searchTasks.Add(t);
                t.Start();
            }

            Task<string> foundTask = null;

            while (searchTasks.AsParallel().Where((t) => !t.IsCompleted).Count() != 0 && foundTask == null)
            {
                foreach (Task<string> task in searchTasks)
                {
                    if (task.IsCompleted && task.Result != null)
                    {
                        foundTask = task;
                        cts.Cancel();
                        break;
                    }
                }
                Thread.Sleep(500);
            }

            if (foundTask != null)
            {
                GamePath = foundTask.Result;

                Settings.Default.GamePath = GamePath;
            }
            else
            {
                throw new IOException("Could not find Bannerlord directory automatically.");
            }
        }

        ~GameProcess()
        {
            process.Kill();
        }
        #endregion
    }


    public class RecursiveFileProcessor
    {
        private string targetFile;

        public RecursiveFileProcessor(string targetFile)
        {
            this.targetFile = targetFile;
        }

        // Process all files in the directory passed in, recurse on any directories
        // that are found, and process the files they contain.
        public string ProcessDirectory(string targetDirectory, CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return null;
            }

            // Process the list of files found in the directory.
            try
            {
                string[] fileEntries = Directory.GetFiles(targetDirectory);
                foreach (string filename in fileEntries)
                {
                    if (ProcessFile(filename.Split('\\').Last()))
                    {
                        return targetDirectory;
                    }
                }

                // Recurse into subdirectories of this directory.
                string[] subdirectoryEntries = Directory.GetDirectories(targetDirectory);
                foreach (string subdirectory in subdirectoryEntries)
                {
                    string result = ProcessDirectory(subdirectory, cancellationToken);
                    if (result != null)
                    {
                        return result;
                    }
                }
            } catch(UnauthorizedAccessException)
            {
                Trace.WriteLine($"Unable to access {targetDirectory}");
            }
            return null;

        }

        // Insert logic for processing found files here.
        public bool ProcessFile(string filename)
        {
            return targetFile == filename;
        }
    }
}
