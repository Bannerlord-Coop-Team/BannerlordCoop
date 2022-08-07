using System;
using System.Diagnostics;
using System.IO;
using Coop.Mod;
using NLog;
using NLog.Config;
using NLog.Targets;
using NLog.Targets.Wrappers;

namespace Coop.Debugging.Logger
{
    public class NLogLogger : ILogger
    {
        private readonly NLog.Logger _logger;
        private readonly bool _isServer;

        /// <summary>
        ///     Initialize debug logging configuration.
        /// </summary>
        public NLogLogger(ICoopServer coopServer = null)
        {
            _logger = LogManager.GetCurrentClassLogger();
            _isServer = coopServer != null;

            LoggingConfiguration config = new LoggingConfiguration();

            FileTarget fileTarget = new FileTarget("logFile")
            {
                FileName = GetLogFilePath()
            };

            AsyncTargetWrapper asyncWrapper = new AsyncTargetWrapper(fileTarget)
            {
                OverflowAction = AsyncTargetWrapperOverflowAction.Block
            };

            config.AddRule(LogLevel.Debug, LogLevel.Fatal, asyncWrapper);
            LogManager.Configuration = config;
        }
    
        /// <summary>
        ///     Get the file path where the logs are saved.
        /// </summary>
        /// <returns>Log file path</returns>
        public String GetLogFilePath()
        {
            return $"{Directory.GetCurrentDirectory()}/Coop_{(_isServer ? "server" : "client")}.log";
        }

        /// <summary>
        ///     Writes the diagnostic message at the <c>Debug</c> level.
        /// </summary>
        /// <param name="message">Log message.</param>
        public void Debug(String message)
        {
            _logger.Debug(message);
        }

        /// <summary>
        ///     Writes the diagnostic message at the <c>Fatal</c> level.
        /// </summary>
        /// <param name="message">Log message.</param>
        public void Fatal(String message)
        {
            _logger.Fatal(message);
        }

        /// <summary>
        ///     Writes the diagnostic message at the <c>Error</c> level.
        /// </summary>
        /// <param name="message">Log message.</param>
        public void Error(String message)
        {
            _logger.Error(message);
        }
        
        /// <summary>
        ///     Stopping the log manager.
        /// </summary>
        public void Dispose()
        {
            LogManager.Shutdown();
        }
    }
}