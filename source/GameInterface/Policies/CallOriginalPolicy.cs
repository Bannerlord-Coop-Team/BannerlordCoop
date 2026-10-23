using Common.Util;
using System;
using System.Threading;

namespace GameInterface.Policies;

/// <summary>
/// Decides whether a patched vanilla method runs on this side, from allow scopes and the session's <see cref="ISyncPolicy"/>.
/// </summary>
public class CallOriginalPolicy
{
    private static int _allThreadsAllowedCount;

    [ThreadStatic]
    private static int _currentOperationAllowedCount;

    internal static bool AreOriginalsAllowedOnAllThreads =>
        Volatile.Read(ref _allThreadsAllowedCount) > 0;

    internal static bool AreOriginalsAllowedForCurrentOperation =>
        _currentOperationAllowedCount > 0;

    /// <summary>
    /// Returns true when the original should run. Outside a session there is no policy to resolve, so originals run.
    /// </summary>
    public static bool IsOriginalAllowed()
    {
        // While using an allowed thread or operation, allow original call
        if (AllowedThread.IsThisThreadAllowed() ||
            AreOriginalsAllowedOnAllThreads ||
            AreOriginalsAllowedForCurrentOperation) return true;

        // ContainerProvider already logs a missing registration once per scope, so logging here floods every patched call
        if (!ContainerProvider.TryResolve<ISyncPolicy>(out var syncPolicy)) return true;

        return syncPolicy.AllowOriginal();
    }

    /// <summary>
    /// Like <see cref="IsOriginalAllowed"/> but ignores <see cref="AllowedThread"/>, so ownership gates still apply on the receive path.
    /// </summary>
    public static bool IsOriginalAllowedForOwnershipGate()
    {
        if (AreOriginalsAllowedOnAllThreads ||
            AreOriginalsAllowedForCurrentOperation) return true;

        if (!ContainerProvider.TryResolve<ISyncPolicy>(out var syncPolicy)) return true;

        return syncPolicy.AllowOriginal();
    }

    /// <summary>
    /// Allows original calls on every thread for the returned scope's lifetime. Use only around
    /// operations, such as the native save loader, that synchronously dispatch patched work to
    /// engine-owned threads and wait for all of it to finish before the scope is disposed.
    /// </summary>
    public static IDisposable AllowOriginalsOnAllThreads() => new AllThreadsScope();

    /// <summary>
    /// Allows original calls made synchronously by the current operation on this thread.
    /// </summary>
    internal static IDisposable AllowOriginalsForCurrentOperation() => new CurrentOperationScope();

    private sealed class CurrentOperationScope : IDisposable
    {
        private int _disposed;

        public CurrentOperationScope()
        {
            _currentOperationAllowedCount++;
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 0)
            {
                _currentOperationAllowedCount--;
            }
        }
    }

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
