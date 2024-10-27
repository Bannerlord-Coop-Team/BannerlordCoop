using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Scaffolderlord
{
	// Honestly could be replaced by Serilog
	public class Logger : ILogger
	{
		private readonly LogLevel _minLogLevel;
		private static readonly object _consoleLock = new object();
		private readonly bool _includeTimestamp;

		public Logger(
			LogLevel minLogLevel = LogLevel.Information,
			bool includeTimestamp = false)
		{
			_minLogLevel = minLogLevel;
			_includeTimestamp = includeTimestamp;
		}

		public IDisposable BeginScope<TState>(TState state) => NullScope.Instance;

		private class NullScope : IDisposable
		{
			public static NullScope Instance { get; } = new NullScope();
			public void Dispose() { }
		}

		public bool IsEnabled(LogLevel logLevel) => logLevel >= _minLogLevel;

		public void Log<TState>(
			LogLevel logLevel,
			EventId eventId,
			TState state,
			Exception? exception,
			Func<TState, Exception?, string> formatter)
		{
			if (!IsEnabled(logLevel))
			{
				return;
			}

			if (formatter == null) throw new ArgumentNullException(nameof(formatter));
			if (state == null) throw new ArgumentNullException(nameof(state));

			var message = formatter(state, exception);
			if (exception != null)
			{
				message += $"{Environment.NewLine}{exception}";
			}

			var output = BuildOutputMessage(logLevel, message);

			lock (_consoleLock)
			{
				Console.ForegroundColor = GetConsoleColor(logLevel);
				Console.WriteLine(output);
				Console.ResetColor();
			}
		}

		private string BuildOutputMessage(LogLevel logLevel, string message)
		{
			var components = new List<string>();

			if (_includeTimestamp)
			{
				var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
				components.Add(timestamp);
			}

			if (logLevel >= LogLevel.Warning)
			{
				components.Add($"[{logLevel}]");
			}
			components.Add(message);

			return string.Join(" ", components);
		}

		private ConsoleColor GetConsoleColor(LogLevel logLevel) => logLevel switch
		{
			LogLevel.Trace => ConsoleColor.Gray,
			LogLevel.Debug => ConsoleColor.Gray,
			LogLevel.Information => ConsoleColor.White,
			LogLevel.Warning => ConsoleColor.Yellow,
			LogLevel.Error => ConsoleColor.Red,
			LogLevel.Critical => ConsoleColor.DarkRed,
			_ => ConsoleColor.White
		};
	}
	public class LoggerProvider : ILoggerProvider
	{
		private readonly LogLevel _minLogLevel;
		private readonly bool _includeTimestamp;

		public LoggerProvider(
			LogLevel minLogLevel = LogLevel.Information,
			bool includeTimestamp = false)
		{
			_minLogLevel = minLogLevel;
			_includeTimestamp = includeTimestamp;
		}

		public ILogger CreateLogger(string categoryName)
		{
			return new Logger(_minLogLevel, _includeTimestamp);
		}

		public void Dispose()
		{

		}
	}

}
