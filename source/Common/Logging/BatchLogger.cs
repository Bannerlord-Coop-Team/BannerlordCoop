using Common.Util;
using Serilog;
using System;
using System.Collections.Concurrent;
using System.Text;
using System.Threading;

namespace Common.Logging;

/// <summary>
/// A class for logging batches of messages of type T.
/// </summary>
public sealed class BatchLogger : IDisposable
{
	// A logger to log the messages.
	private static readonly ILogger Logger = LogManager.GetLogger<BatchLogger>();
	// A task to poll for messages to log.
	private readonly Poller poller;
	// The number of milliseconds to wait between polls.
	private readonly TimeSpan pollInterval;

	private readonly ConcurrentDictionary<string, int> LogMap = new ConcurrentDictionary<string, int>();

    /// <summary>
    /// Constructs a BatchLogger.
    /// </summary>
    /// <param name="level">The log level to use when logging the messages.</param>
	/// <param name="waitMilliseconds">The number of milliseconds to wait between polls (optional, default is 1000).</param>
	public BatchLogger(TimeSpan pollInterval)
	{
        this.pollInterval = pollInterval;
        poller = new Poller(Poll, pollInterval);
        poller.Start();
    }

	/// <summary>
	/// Logs a message.
	/// </summary>
	public void LogOne(Type messageType)
	{
		var messageName = messageType.Name;

        LogMap.AddOrUpdate(messageName, 1, (name, value) => Interlocked.Increment(ref value));
    }

    // A method to poll for messages to log.
    private void Poll(TimeSpan dt)
	{
		if (LogMap.Count == 0) return;

        var stringBuilder = new StringBuilder();
		stringBuilder.AppendLine($"Batch Logged messaged (every {dt.Seconds} seconds)");

        foreach (var messageName in LogMap.Keys)
		{
			if (LogMap.TryRemove(messageName, out var count) && count > 0)
			{
                stringBuilder.AppendLine($"\t{messageName}: {count} messages per {dt.Seconds} second(s)");
            }
		}

		Logger.Information(stringBuilder.ToString());
    }

	/// <summary>
	/// Disposes of the BatchLogger.
	/// </summary>
	public void Dispose()
	{
		poller.Stop();
	}
}
