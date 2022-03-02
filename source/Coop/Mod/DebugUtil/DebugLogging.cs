using System.Diagnostics;
using System.IO;
using System.Reflection;
using NLog;
using NLog.Config;
using NLog.Layouts;
using NLog.Targets;
using NLog.Targets.Wrappers;
using TaleWorlds.Engine;

namespace Coop.Mod
{
    public static class DebugLogging
    {
        /// <summary>
        /// Initialize debug logging configuration.
        /// </summary>
        [Conditional("DEBUG")]
        public static void Initialize()
        {
            RegisterCustomTargets();

            LoggingConfiguration config = new LoggingConfiguration();

            FileTarget fileTarget = new FileTarget("logFile")
            {
                FileName = GetLogFileName()
            };

            AsyncTargetWrapper asyncWrapper = new AsyncTargetWrapper(fileTarget)
            {
                OverflowAction = AsyncTargetWrapperOverflowAction.Block
            };

            NLogViewerTarget viewerTarget = new NLogViewerTarget("viewer")
            {
                Address = "udp://127.0.0.1:9999",
                IncludeSourceInfo = true
            };

            MbLogTarget MBLogTarget = new MbLogTarget
            {
                Layout = Layout.FromString("[${level:uppercase=true}] ${message}")
            };

            config.AddRule(LogLevel.Debug, LogLevel.Fatal, asyncWrapper);
            config.AddRule(LogLevel.Trace, LogLevel.Fatal, viewerTarget);
            config.AddRule(LogLevel.Info, LogLevel.Fatal, MBLogTarget);

            LogManager.Configuration = config;
        }

        /// <summary>
        /// Register custom targets used by NLog.
        /// </summary>
        private static void RegisterCustomTargets()
        {
            Target.Register<MbLogTarget>("MbLog");
        }

        /// <summary>
        /// Get the file name based on the process argument line.
        /// </summary>
        /// <returns></returns>
        private static string GetLogFileName()
        {
            return $"Coop_{(Utilities.GetFullCommandLineString().Contains("/server") ? "server" : "client")}.txt";
        }

        /// <summary>
        /// Flush and close down NLog threads and timers.
        /// </summary>
        [Conditional("DEBUG")]
        public static void Shutdown()
        {
            LogManager.Shutdown();
        }
    }
}
