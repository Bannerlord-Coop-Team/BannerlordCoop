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
    private readonly BatchLogger batchLogger = new BatchLogger(TimeSpan.FromSeconds(1));

    public MessageLogger(ILogger logger)
    {
        this.logger = logger;
    }

    public void LogMessage(object source, Type messageType)
    {
        if (messageType.GetCustomAttribute<DontLogMessageAttribute>() != null) return;

        BatchLog(messageType);
        return;

        if (messageType.GetCustomAttribute<BatchLogMessageAttribute>() != null)
        {
            return;
        }

        logger.Verbose("Publishing {msgName} from {sourceName}", messageType.Name, source?.GetType().Name ?? "Static Method");
    }

    private void BatchLog(Type messageType)
    {
        batchLogger.LogOne(messageType);
    }
}
