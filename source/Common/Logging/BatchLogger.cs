using Common.Util;
using Serilog;
using Serilog.Events;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Common.Logging;

/// <summary>
/// A class for logging batches of messages of type T.
/// </summary>
public sealed class BatchLogger : IDisposable
	{
    private readonly string messageName;
    private int messageCount = 0;
	// A logger to log the messages.
	private static readonly ILogger Logger = LogManager.GetLogger<BatchLogger>();
	// A task to poll for messages to log.
	private readonly Poller poller;
	// The number of milliseconds to wait between polls.
	private readonly TimeSpan pollInterval;

    /// <summary>
    /// Constructs a BatchLogger.
    /// </summary>
    /// <param name="level">The log level to use when logging the messages.</param>
	/// <param name="waitMilliseconds">The number of milliseconds to wait between polls (optional, default is 1000).</param>
	public BatchLogger(string messageName, TimeSpan pollInterval)
	{
			this.messageName = messageName;
        this.pollInterval = pollInterval;
        poller = new Poller(Poll, pollInterval);
        poller.Start();

    }

	/// <summary>
	/// Logs a message.
	/// </summary>
	public void LogOne() => Interlocked.Increment(ref messageCount);

    // A method to poll for messages to log.
    private void Poll(TimeSpan dt)
		{
        if (messageCount > 0)
        {
            Logger.Information("{messageCount} {messageName} messages has been received in {milliseconds}ms", messageCount, messageName, pollInterval.Milliseconds);

            Interlocked.Exchange(ref messageCount, 0);
        }
    }

	/// <summary>
	/// Disposes of the BatchLogger.
	/// </summary>
	public void Dispose()
	{
		poller.Stop();
	}
}
