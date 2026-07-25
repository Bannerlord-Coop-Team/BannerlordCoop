using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Management;
using System.Threading;
using EnvDTE80;

namespace DebugServerLauncher
{
    /// <summary>
    /// Starts the .stage-win coop dedicated server and auto-attaches the running
    /// Visual Studio's CoreCLR debugger to the engine process.
    ///
    /// Why this exists: the Coop projects target .NET Framework 4.7.2, so F5 on
    /// BannerlordCoopServer.exe attaches VS's Desktop CLR engine to a .NET 6
    /// process and dies with "the target process loaded the CoreCLR runtime".
    /// The exe is also just a wrapper — all Coop/GameInterface code runs in the
    /// child dotnet.exe it spawns (the native DS engine), which VS never
    /// auto-attaches to. This launcher (net472 itself, so F5 attaches to IT
    /// cleanly) starts the server, waits for the engine child, and attaches the
    /// "Managed (.NET Core, .NET 5+)" engine to it via DTE automation.
    ///
    /// Wired as source\Coop's DebugAutoConnect StartProgram. From a terminal,
    /// run tools\debug-server.ps1 (builds this project when stale, then runs it).
    ///
    /// With --no-attach the debug concessions are dropped and the server runs
    /// stock — TUI and Watchdog crash dumps — with this process staying silent;
    /// only the kill-on-close guarantee (and the F5 keybinding) remain.
    ///
    /// The server goes into a kill-on-close job object: when this launcher dies
    /// (VS Stop Debugging, console closed), launcher AND engine die with it.
    /// Server output appears in this console; Ctrl+C stops everything.
    /// </summary>
    internal static class Program
    {
        // CoRegisterMessageFilter (VisualStudioAttacher) requires an STA thread.
        [STAThread]
        private static int Main(string[] args)
        {
            Options options;
            try
            {
                options = Options.Parse(args);
            }
            catch (ArgumentException ex)
            {
                Console.Error.WriteLine("[debug-server] bad arguments: " + ex.Message);
                Options.PrintUsage();
                return 2;
            }
            if (options.ShowHelp)
            {
                Options.PrintUsage();
                return 0;
            }

            string serverExe = options.ServerExe ?? FindServerExe();
            if (serverExe == null || !File.Exists(serverExe))
            {
                Console.Error.WriteLine("[debug-server] server exe not found" +
                    (serverExe == null ? " (no .stage-win\\BannerlordCoopServer.exe above this exe; pass --server-exe)" : ": " + serverExe));
                return 2;
            }

            IntPtr job = JobObject.CreateKillOnCloseJob();
            if (job == IntPtr.Zero)
            {
                Console.Error.WriteLine("[debug-server] warning: could not create the kill-on-close job; a hard-stopped run may leave the server running");
            }

            var serverArgs = new List<string>();
            if (!options.NoAttach)
            {
                // Debug mode only:
                //   --no-tui       this launcher must print attach progress/results to
                //                  the shared console, which would garble the TUI.
                //   --no-watchdog  the engine's Watchdog.exe crash collector attaches
                //                  to the engine as a debugger, occupying the single
                //                  debugger slot — VS attach fails with 0x8971001E
                //                  while it runs. A debug session IS the crash handler.
                // With --no-attach the server runs stock (TUI + Watchdog, i.e. crash
                // dumps); pass --no-watchdog through to keep the debugger slot free
                // for a later manual attach.
                serverArgs.Add("--no-tui");
                serverArgs.Add("--no-watchdog");
            }
            if (options.Trace)
            {
                serverArgs.Add("--trace");
            }
            serverArgs.AddRange(options.Passthrough);

            // net472 has no ProcessStartInfo.ArgumentList; quote args by hand.
            string argumentLine = string.Join(" ", serverArgs.ConvertAll(QuoteArg));
            Console.WriteLine("[debug-server] starting: " + serverExe + " " + argumentLine);
            Process server;
            var psi = new ProcessStartInfo
            {
                FileName = serverExe,
                Arguments = argumentLine,
                UseShellExecute = false,
            };
            try
            {
                server = Process.Start(psi);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("[debug-server] could not start the server: " + ex.Message);
                return 5;
            }
            if (!JobObject.Add(job, server.Handle))
            {
                Console.Error.WriteLine("[debug-server] warning: could not add the server to the job; it may outlive this console if hard-stopped");
            }

            // Ctrl+C goes to the whole console group; the server's launcher handles
            // its own shutdown — we just stay alive long enough to see it exit.
            Console.CancelKeyPress += (sender, e) => e.Cancel = true;

            if (options.NoAttach)
            {
                // Stock run: the server owns the console (its TUI engages on an
                // interactive console) — print nothing while it lives, just keep
                // the job-object guarantee and pass its exit code through.
                server.WaitForExit();
                if (server.ExitCode != 0)
                {
                    Console.Error.WriteLine("[debug-server] server exited with code " + server.ExitCode +
                        " (2=load failure, 4=Coop module verification, 5=engine launch failure)");
                }
                return server.ExitCode;
            }

            // The server launcher re-stages Modules\Coop (devModuleRoot), then
            // spawns the engine as a child dotnet.exe; that child is where every
            // Coop breakpoint lives.
            int enginePid = WaitForEngineChild(server, options.ChildTimeoutSec);
            if (enginePid == 0 && server.HasExited)
            {
                Console.Error.WriteLine("[debug-server] server exited (code " + server.ExitCode + ") before the engine started. " +
                    "2=load failure, 4=Coop module verification, 5=engine launch failure - see its output above.");
                return server.ExitCode;
            }

            if (enginePid != 0)
            {
                AttachVisualStudio(enginePid, options);
            }
            else
            {
                Console.Error.WriteLine("[debug-server] warning: no engine child (dotnet.exe) appeared within " +
                    options.ChildTimeoutSec + "s; running without attach");
            }

            Console.WriteLine("[debug-server] server console follows; Ctrl+C (or VS Stop Debugging) stops server + engine");
            server.WaitForExit();
            return server.ExitCode;
        }

        private static void AttachVisualStudio(int enginePid, Options options)
        {
            string manualHint = "Attach manually: Debug > Attach to Process > dotnet.exe PID " + enginePid +
                ", engine 'Managed (.NET Core, .NET 5+)'.";
            Console.WriteLine("[debug-server] engine process: dotnet.exe PID " + enginePid + " - attaching Visual Studio...");
            int instanceCount;
            DTE2 dte = VisualStudioAttacher.FindVisualStudio(options.SolutionMatch, out instanceCount);
            if (dte == null)
            {
                Console.Error.WriteLine("[debug-server] warning: no running Visual Studio found to attach (" +
                    instanceCount + " instances seen, none with '" + options.SolutionMatch + "'). " + manualHint);
                return;
            }
            string error = VisualStudioAttacher.Attach(dte, enginePid, options.AttachTimeoutSec);
            if (error == null)
            {
                Console.WriteLine("[debug-server] Visual Studio attached (CoreCLR) to engine PID " + enginePid +
                    " - breakpoints in Coop code will bind");
            }
            else
            {
                Console.Error.WriteLine("[debug-server] warning: auto-attach failed: " + error + ". " + manualHint);
            }
        }

        private static string QuoteArg(string arg)
        {
            return arg.Contains(" ") || arg.Contains("\"")
                ? "\"" + arg.Replace("\"", "\\\"") + "\""
                : arg;
        }

        /// <summary>Polls for the server's dotnet.exe child; 0 if none appeared.</summary>
        private static int WaitForEngineChild(Process server, int timeoutSec)
        {
            DateTime deadline = DateTime.UtcNow.AddSeconds(timeoutSec);
            while (DateTime.UtcNow < deadline)
            {
                if (server.HasExited)
                {
                    return 0;
                }
                using (var searcher = new ManagementObjectSearcher(
                    "SELECT ProcessId FROM Win32_Process WHERE ParentProcessId=" + server.Id + " AND Name='dotnet.exe'"))
                {
                    foreach (ManagementBaseObject row in searcher.Get())
                    {
                        return (int)(uint)row["ProcessId"];
                    }
                }
                Thread.Sleep(300);
            }
            return 0;
        }

        /// <summary>
        /// Walks up from this exe (tools\DebugServerLauncher\bin\...) to the repo
        /// root and returns &lt;root&gt;\.stage-win\BannerlordCoopServer.exe.
        /// </summary>
        private static string FindServerExe()
        {
            string dir = AppDomain.CurrentDomain.BaseDirectory;
            while (dir != null)
            {
                string candidate = Path.Combine(dir, ".stage-win", "BannerlordCoopServer.exe");
                if (File.Exists(candidate))
                {
                    return candidate;
                }
                dir = Path.GetDirectoryName(dir.TrimEnd(Path.DirectorySeparatorChar));
            }
            return null;
        }
    }

    internal sealed class Options
    {
        public string ServerExe { get; private set; }
        public string SolutionMatch { get; private set; } = "Coop.sln";
        public bool NoAttach { get; private set; }
        public bool Trace { get; private set; }
        public int ChildTimeoutSec { get; private set; } = 60;
        public int AttachTimeoutSec { get; private set; } = 30;
        public bool ShowHelp { get; private set; }
        public List<string> Passthrough { get; } = new List<string>();

        public static Options Parse(string[] args)
        {
            var options = new Options();
            for (int i = 0; i < args.Length; i++)
            {
                switch (args[i].ToLowerInvariant())
                {
                    case "-h":
                    case "--help":
                    case "/?":
                        options.ShowHelp = true;
                        break;
                    case "--server-exe":
                        options.ServerExe = RequireValue(args, ref i);
                        break;
                    case "--solution-match":
                        options.SolutionMatch = RequireValue(args, ref i);
                        break;
                    case "--no-attach":
                        options.NoAttach = true;
                        break;
                    case "--trace":
                        options.Trace = true;
                        break;
                    case "--child-timeout":
                        options.ChildTimeoutSec = int.Parse(RequireValue(args, ref i));
                        break;
                    case "--attach-timeout":
                        options.AttachTimeoutSec = int.Parse(RequireValue(args, ref i));
                        break;
                    default:
                        // Anything else goes through to BannerlordCoopServer.exe
                        // (e.g. --port 7211), which validates its own options.
                        options.Passthrough.Add(args[i]);
                        break;
                }
            }
            return options;
        }

        private static string RequireValue(string[] args, ref int i)
        {
            if (i + 1 >= args.Length)
            {
                throw new ArgumentException("missing value after " + args[i]);
            }
            return args[++i];
        }

        public static void PrintUsage()
        {
            Console.WriteLine("DebugServerLauncher - start the .stage-win coop server and attach VS to the engine");
            Console.WriteLine();
            Console.WriteLine("Usage: DebugServerLauncher.exe [options] [-- passthrough to BannerlordCoopServer.exe]");
            Console.WriteLine();
            Console.WriteLine("  --server-exe <path>      server exe (default: <repo>\\.stage-win\\BannerlordCoopServer.exe)");
            Console.WriteLine("  --solution-match <text>  pick the VS instance whose solution path contains this (default: Coop.sln)");
            Console.WriteLine("  --no-attach              run the server stock (TUI + Watchdog crash dumps), no debugger;");
            Console.WriteLine("                           add --no-watchdog to keep the slot free for a manual attach later");
            Console.WriteLine("  --trace                  pass --trace to the server (MonoMod + crash-dump diagnostics)");
            Console.WriteLine("  --child-timeout <sec>    max wait for the engine child (default: 60)");
            Console.WriteLine("  --attach-timeout <sec>   max wait for the VS attach (default: 30)");
            Console.WriteLine("  -h, --help               show this help");
            Console.WriteLine();
            Console.WriteLine("Unrecognized options pass through to BannerlordCoopServer.exe (e.g. --port 7211).");
            Console.WriteLine();
            Console.WriteLine("Modes:  (default)     no TUI, no Watchdog, VS auto-attaches to the engine");
            Console.WriteLine("        --no-attach   stock server (TUI, Watchdog) under the kill-on-close job");
        }
    }
}
