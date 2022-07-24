using System;
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
        /// Get the file name based on the process argument line and the "COOP_LOG" environment variable to specify the folder where the logs are saved. If no 
        /// environment variable is set, the output will be on the Bannerlord.exe side by default if you are using Steam it 
        /// will be on this folder: "%Steam%\steamapps\common\Mount & Blade II Bannerlord\bin\Win64_Shipping_Client"
        /// </summary>
        /// <returns></returns>
        private static string GetLogFileName()
        {
            return $"{Directory.GetCurrentDirectory()}/Coop_{(Utilities.GetFullCommandLineString().Contains("/server") ? "server" : "client")}.log";
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
