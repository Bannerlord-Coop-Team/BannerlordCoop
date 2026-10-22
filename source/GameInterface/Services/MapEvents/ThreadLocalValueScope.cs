using System;
using System.Threading;

namespace GameInterface.Services.MapEvents;

/// <summary>Sets a thread-local value for the scope's lifetime and puts the previous value back on dispose, so nested scopes unwind correctly.</summary>
internal sealed class ThreadLocalValueScope<T> : IDisposable
{
    private readonly ThreadLocal<T> slot;
    private readonly T previous;
    private bool disposed;

    // Remembers the slot's current value on this thread, then replaces it with value.
    public ThreadLocalValueScope(ThreadLocal<T> slot, T value)
    {
        this.slot = slot;
        previous = slot.Value;
        slot.Value = value;
    }

    // Restores the value seen when the scope opened; later calls do nothing.
    public void Dispose()
    {
        if (disposed) return;

        disposed = true;
        slot.Value = previous;
    }
}
