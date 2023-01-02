using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Serilog;
using Serilog.Events;

namespace Common.Logging
{
	/// <summary>
	/// A class for logging batches of messages of type T.
	/// </summary>
	/// <typeparam name="T">The type of object you want to be counted</typeparam>
	public sealed class BatchLogger<T> : IDisposable
	{
		// A concurrent dictionary to store messages of type T and their counts.
		private readonly ConcurrentDictionary<T, int> _messages = new ConcurrentDictionary<T, int>();
		// A logger to log the messages.
		private readonly ILogger _logger = LogManager.GetLogger<BatchLogger<T>>();
		// The log level to use when logging the messages.
		private readonly LogEventLevel _level;
		// A cancellation token source to cancel the poller task.
		private readonly CancellationTokenSource _cancellation = new CancellationTokenSource();
		// A task to poll for messages to log.
		private readonly Task _poller;
		// The number of milliseconds to wait between polls.
		private readonly int _waitMilliseconds;

		/// <summary>
		/// Constructs a BatchLogger.
		/// </summary>
		/// <param name="level">The log level to use when logging the messages.</param>
		/// <param name="waitMilliseconds">The number of milliseconds to wait between polls (optional, default is 1000).</param>
		public BatchLogger(LogEventLevel level, int waitMilliseconds = 1000)
		{
			_level = level;
			_waitMilliseconds = waitMilliseconds;
			_poller = Task.Factory.StartNew(Poll, _cancellation.Token);
		}

		/// <summary>
		/// Logs a message of type T.
		/// </summary>
		/// <param name="value">The message to log.</param>
		public void Log(T value) => _messages.AddOrUpdate(value,
			1, (_, i) => ++i);

		// A method to poll for messages to log.
		private void Poll()
		{
			// Keep polling until cancellation is requested.
			while (!_cancellation.IsCancellationRequested)
			{
				// Sleep for the specified number of milliseconds.
				Thread.Sleep(_waitMilliseconds);
				// Iterate through the keys in the messages dictionary.
				foreach (var key in _messages.Keys)
				{
					// If the message can be removed from the dictionary, log it.
					if (_messages.TryRemove(key, out var value))
						_logger.Write(_level, "{Type} received {Times} times in last {WaitMilliseconds}ms",
							key, value, _waitMilliseconds);
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
			while (!_poller.IsCompleted)
			{
				Thread.Sleep(1);
			}
			// Dispose of the cancellation token source.
			_cancellation?.Dispose();
		}
	}
}
