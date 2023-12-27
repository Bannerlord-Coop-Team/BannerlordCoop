using Common.Logging.Attributes;
using Common.Messaging;
using Serilog;
using System;
using System.Collections.Concurrent;
using System.Reflection;

namespace Common.Logging;

/// <summary>
/// Logger used for logging <see cref="IMessage"/>
/// </summary>
/// <remarks>
/// Default logging behavior is to log every message.
/// Handles <see cref="DontLogMessageAttribute"/> (ignores logging)
/// and <seealso cref="BatchLogMessageAttribute"/> (logs in batches). 
/// </remarks>
public class MessageLogger
{
    private readonly ILogger logger;

    public MessageLogger(ILogger logger)
    {
        this.logger = logger;
    }

    public void LogMessage(Type messageType)
    {
        if (messageType.GetCustomAttribute<DontLogMessageAttribute>() != null) return;

        if (messageType.GetCustomAttribute<BatchLogMessageAttribute>() != null)
        {
            BatchLog(messageType);
            return;
        }

        logger.Verbose("Publishing {msgName} from {sourceName}", messageType.Name, messageType?.GetType().Name);
    }

    private ConcurrentDictionary<Type, BatchLogger> loggers = new ConcurrentDictionary<Type, BatchLogger>();
    private void BatchLog(Type messageType)
    {
        if (loggers.TryGetValue(messageType, out var batchLogger))
        {
            batchLogger.LogOne();
        }
        else
        {
            var newBatchLogger = new BatchLogger(messageType.Name, TimeSpan.FromSeconds(1));
            if (loggers.TryAdd(messageType, newBatchLogger))
            {
                logger.Error("Unable to add {messageType} to batch loggers", messageType);
                return;
            }

            newBatchLogger.LogOne();
        }
    }
}
