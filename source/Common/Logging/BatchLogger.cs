using Serilog;
using Serilog.Events;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Common.Logging
{
    /// <summary>
    /// A class for logging batches of messages of type T.
    /// </summary>
    public sealed class BatchLogger : IDisposable
	{
        private readonly string messageName;
        private int messageCount = 0;
		// A logger to log the messages.
		private static readonly ILogger Logger = LogManager.GetLogger<BatchLogger>();
		// The log level to use when logging the messages.
		private readonly LogEventLevel level;
		// A cancellation token source to cancel the poller task.
		private readonly CancellationTokenSource _cancellation = new CancellationTokenSource();
		// A task to poll for messages to log.
		private readonly Task poller;
		// The number of milliseconds to wait between polls.
		private readonly int waitMilliseconds;

        /// <summary>
        /// Constructs a BatchLogger.
        /// </summary>
        /// <param name="level">The log level to use when logging the messages.</param>
		/// <param name="waitMilliseconds">The number of milliseconds to wait between polls (optional, default is 1000).</param>
		public BatchLogger(string messageName, LogEventLevel level, int waitMilliseconds = 1000)
		{
			this.messageName = messageName;
			this.level = level;
			this.waitMilliseconds = waitMilliseconds;
			poller = Task.Factory.StartNew(Poll, _cancellation.Token);
		}

		/// <summary>
		/// Logs a message.
		/// </summary>
		public void LogOne() => Interlocked.Increment(ref messageCount);

        // A method to poll for messages to log.
        private async void Poll()
		{
			// Keep polling until cancellation is requested.
			while (!_cancellation.IsCancellationRequested)
			{
				// Sleep for the specified number of milliseconds.
				await Task.Delay(waitMilliseconds);

				if(messageCount > 0)
				{
                    Logger.Information("{messageCount} {messageName} messages has been received in {milliseconds}ms", messageCount, messageName, waitMilliseconds);

                    Interlocked.Exchange(ref messageCount, 0);
                }
			}
		}

		/// <summary>
		/// Disposes of the BatchLogger.
		/// </summary>
		public void Dispose()
		{
			// Cancel the poller task.
			if (_cancellation.IsCancellationRequested)
				return;
			_cancellation.Cancel();
			// Wait for the poller task to complete.
			while (!poller.IsCompleted)
			{
				Thread.Sleep(1);
			}

			// Dispose of the cancellation token source.
			_cancellation?.Dispose();
		}
	}
}
