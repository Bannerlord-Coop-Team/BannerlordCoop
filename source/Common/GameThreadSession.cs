using System;
using System.Threading;

namespace Common;

/// <summary>
/// Owns game-thread work queued by one co-op container lifetime.
/// </summary>
public interface IGameThreadSession : IDisposable
{
    bool IsActive { get; }
    WaitHandle CancellationWaitHandle { get; }
    IDisposable Activate();
    void Cancel();
}

/// <inheritdoc cref="IGameThreadSession"/>
public sealed class GameThreadSession : IGameThreadSession
{
    private readonly ManualResetEvent cancellation = new ManualResetEvent(false);
    private readonly object gate = new object();
    private int cancelled;
    private bool disposed;

    public bool IsActive => Volatile.Read(ref cancelled) == 0;
    public WaitHandle CancellationWaitHandle => cancellation;

    public IDisposable Activate() => GameThread.ActivateSession(this);

    public void Cancel()
    {
        lock (gate)
        {
            if (cancelled != 0) return;

            Volatile.Write(ref cancelled, 1);
            cancellation.Set();
        }
    }

    public void Dispose()
    {
        lock (gate)
        {
            if (disposed) return;

            if (cancelled == 0)
            {
                Volatile.Write(ref cancelled, 1);
                cancellation.Set();
            }

            disposed = true;
            cancellation.Dispose();
        }
    }
}
