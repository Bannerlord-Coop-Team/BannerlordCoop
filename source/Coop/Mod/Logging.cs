using System.Diagnostics;
using System.IO;
using System.Reflection;
using NLog;
using NLog.Config;
using NLog.Targets;

namespace Common
{
    public static class Logging
    {
        public static void Init(Target[] appSpecificTargets)
        {
            LoggingConfiguration config = new LoggingConfiguration();

            // Targets
            FileTarget logFile = new FileTarget("logFile")
            {
                FileName = GetLogFileName()
            };
            // DebuggerTarget is disabled because it significantly slows down performance
            // DebuggerTarget debugOutput = new DebuggerTarget("debugOutput");
            NLogViewerTarget viewer = new NLogViewerTarget("viewer")
            {
                Address = "udp://127.0.0.1:9999",
                IncludeSourceInfo = true
            };

            config.AddRule(LogLevel.Info, LogLevel.Fatal, logFile);
            // config.AddRule(LogLevel.Debug, LogLevel.Fatal, debugOutput);
            config.AddRule(LogLevel.Trace, LogLevel.Fatal, viewer);
            foreach (Target t in appSpecificTargets)
            {
                config.AddRule(LogLevel.Info, LogLevel.Fatal, t);
            }

            LogManager.Configuration = config;
        }

        private static string GetLogFileName()
        {
            int iNrOfInstances = Process.GetProcessesByName(
                                            Path.GetFileNameWithoutExtension(
                                                Assembly.GetEntryAssembly().Location))
                                        .Length;
            return $"Coop_{iNrOfInstances - 1}.txt";
        }
    }
}
