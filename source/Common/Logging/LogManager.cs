using System;
using Serilog;

namespace Common.Logging
{
	public static class LogManager
	{
		public static LoggerConfiguration Configuration { get; } = new LoggerConfiguration();
		private static Lazy<ILogger> _logger = new Lazy<ILogger>(() => Configuration.CreateLogger());

		public static ILogger GetLogger<T>() => _logger.Value
			.ForContext<T>();
	}
}
