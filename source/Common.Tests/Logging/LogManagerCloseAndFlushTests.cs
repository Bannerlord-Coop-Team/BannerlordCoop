using Common.Logging;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using System.Diagnostics;
using System.Threading;
using Xunit;

namespace Common.Tests.Logging;

public class LogManagerCloseAndFlushTests
{
    private static readonly TimeSpan Generous = TimeSpan.FromSeconds(10);

    [Fact]
    public void CloseAndFlush_CreatedLogger_DisposesItsSinks()
    {
        var sink = new DisposableSink();
        var logger = new Lazy<ILogger>(() => new LoggerConfiguration().WriteTo.Sink(sink).CreateLogger());
        logger.Value.Information("last line");

        Assert.True(LogManager.CloseAndFlush(logger, Generous));

        Assert.True(sink.Disposed);
    }

    [Fact]
    public void CloseAndFlush_LoggerNeverCreated_DoesNotCreateIt()
    {
        var logger = new Lazy<ILogger>(() => new LoggerConfiguration().CreateLogger());

        Assert.True(LogManager.CloseAndFlush(logger, Generous));

        Assert.False(logger.IsValueCreated);
    }

    [Fact]
    public void CloseAndFlush_SinkSlowerThanTheLimit_ReturnsFalseAtTheLimit()
    {
        using var release = new ManualResetEventSlim();
        var sink = new DisposableSink(release);
        var logger = new Lazy<ILogger>(() => new LoggerConfiguration().WriteTo.Sink(sink).CreateLogger());
        logger.Value.Information("last line");

        var watch = Stopwatch.StartNew();
        var flushed = LogManager.CloseAndFlush(logger, TimeSpan.FromMilliseconds(100));
        watch.Stop();
        release.Set();

        Assert.False(flushed);
        Assert.True(watch.Elapsed < TimeSpan.FromSeconds(5), "waited " + watch.Elapsed);
    }

    [Fact]
    public void CloseAndFlush_SinkThrowsOnDispose_ReturnsWithoutThrowing()
    {
        var logger = new Lazy<ILogger>(() => new ThrowingDisposeLogger());
        _ = logger.Value;

        var exception = Record.Exception(() => LogManager.CloseAndFlush(logger, Generous));

        Assert.Null(exception);
    }

    private sealed class DisposableSink : ILogEventSink, IDisposable
    {
        private readonly ManualResetEventSlim waitBeforeDispose;

        public DisposableSink(ManualResetEventSlim waitBeforeDispose = null) => this.waitBeforeDispose = waitBeforeDispose;

        public bool Disposed { get; private set; }

        public void Emit(LogEvent logEvent)
        {
        }

        public void Dispose()
        {
            waitBeforeDispose?.Wait();
            Disposed = true;
        }
    }

    private sealed class ThrowingDisposeLogger : ILogger, IDisposable
    {
        public void Write(LogEvent logEvent)
        {
        }

        public void Dispose() => throw new InvalidOperationException("sink failed to close");
    }
}
