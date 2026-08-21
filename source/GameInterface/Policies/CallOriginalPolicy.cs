using Common.Logging;
using Common.Util;
using Serilog;
using System;
using System.Threading;

namespace GameInterface.Policies;

public class CallOriginalPolicy
{
    private static readonly ILogger Logger = LogManager.GetLogger<CallOriginalPolicy>();

    private static int _allThreadsAllowedCount;

    internal static bool AreOriginalsAllowedOnAllThreads =>
        Volatile.Read(ref _allThreadsAllowedCount) > 0;

    public static bool IsOriginalAllowed()
    {
        // While using an allowed thread or operation, allow original call
        if (AllowedThread.IsThisThreadAllowed() || AreOriginalsAllowedOnAllThreads) return true;

        if (ContainerProvider.TryResolve<ISyncPolicy>(out var syncPolicy) == false)
        {
            Logger.Error("Unable to resolve {name}", nameof(ISyncPolicy));
            return true;
        }

        if (syncPolicy.AllowOriginal()) return true;

        return false;
    }

    /// <summary>
    /// Allows original calls on every thread for the returned scope's lifetime. Use only around
    /// operations, such as the native save loader, that synchronously dispatch patched work to
    /// engine-owned threads and wait for all of it to finish before the scope is disposed.
    /// </summary>
    public static IDisposable AllowOriginalsOnAllThreads() => new AllThreadsScope();

    private sealed class AllThreadsScope : IDisposable
    {
        private int _disposed;

        public AllThreadsScope()
        {
            Interlocked.Increment(ref _allThreadsAllowedCount);
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 0)
            {
                Interlocked.Decrement(ref _allThreadsAllowedCount);
            }
        }
    }
}
