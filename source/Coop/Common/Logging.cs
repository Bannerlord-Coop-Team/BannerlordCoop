using System.Diagnostics;
using System.IO;
using System.Reflection;
using NLog;
using NLog.Config;
using NLog.Targets;

namespace Coop.Common
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
            DebuggerTarget debugOutput = new DebuggerTarget("debugOutput");

            config.AddRule(LogLevel.Info, LogLevel.Fatal, logFile);
            config.AddRule(LogLevel.Trace, LogLevel.Fatal, debugOutput);
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
