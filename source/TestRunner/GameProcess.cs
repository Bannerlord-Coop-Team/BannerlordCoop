using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;


namespace TestRunner
{
    public enum GameType
    {
        Host,
        Client
    }

    class GameProcess
    {

        Configuration config = ConfigurationManager.OpenExeConfiguration(Application.ExecutablePath);
        string GamePath = ConfigurationManager.OpenExeConfiguration(Application.ExecutablePath).AppSettings.Settings?["GamePath"].Value;
        readonly Process process;

        [DllImport("User32.dll")]
        static extern int SetForegroundWindow(IntPtr point);
        public GameProcess(GameType type)
        {
            if (GamePath == null)
            {
                FindGamePath();
            }

            ProcessStartInfo pInfo = new ProcessStartInfo();
            pInfo.FileName = GamePath + @"\Bannerlord.exe";
            pInfo.WorkingDirectory = GamePath;

            switch (type)
            {
                case GameType.Host:
                    pInfo.Arguments = "/singleplayer /server _MODULES_*Native*SandBoxCore*CustomBattle*SandBox*StoryMode*Coop*_MODULES_";
                    break;
                case GameType.Client:
                    pInfo.Arguments = "/singleplayer /client _MODULES_*Native*SandBoxCore*CustomBattle*SandBox*StoryMode*Coop*_MODULES_";
                    break;
            }

            process = Process.Start(pInfo);

            if (process.MainWindowTitle == "Safe Mode")
            {
                SetForegroundWindow(process.MainWindowHandle);
                SendKeys.SendWait("{TAB}");
                SendKeys.SendWait("{ENTER}");
            }
        }

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

            

            GamePath = foundTask.Result;
            string key = "GamePath";
            config.AppSettings.Settings.Add(key, GamePath);
            config.Save(ConfigurationSaveMode.Minimal);
        }

        ~GameProcess()
        {
            process.Kill();
        }
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
                Console.WriteLine($"Unable to access {targetDirectory}");
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
