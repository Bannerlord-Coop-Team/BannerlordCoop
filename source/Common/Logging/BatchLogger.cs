using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using NLog;

namespace Common.Logging
{
	public sealed class BatchLogger<T> : IDisposable
	{
		private readonly ConcurrentDictionary<T, int> _messages = new ConcurrentDictionary<T, int>();
		private readonly Logger _logger = LogManager.GetCurrentClassLogger();
		private readonly LogLevel _level;
		private readonly CancellationTokenSource _cancellation = new CancellationTokenSource();
		private readonly Task _poller;
		private readonly int _waitMilliseconds;

		public BatchLogger(LogLevel level, int waitMilliseconds = 1000)
		{
			_level = level;
			_waitMilliseconds = waitMilliseconds;
			_poller = Task.Factory.StartNew(Poll, _cancellation.Token);
		}

		public void Log(T value) => _messages.AddOrUpdate(value,
			1, (_, i) => ++i);

		private void Poll()
		{
			while (!_cancellation.IsCancellationRequested)
			{
				Thread.Sleep(_waitMilliseconds);
				foreach (var key in _messages.Keys)
				{
					if (_messages.TryRemove(key, out var value))
						_logger.Log(_level, "{Type} received {Times} times in last {WaitMilliseconds}ms",
							key, value, _waitMilliseconds);
				}
			}
		}

		public void Dispose()
		{
			_cancellation.Cancel();
			while (!_poller.IsCompleted)
			{
				Thread.Sleep(1);
			}
			_cancellation?.Dispose();
		}
	}
}
