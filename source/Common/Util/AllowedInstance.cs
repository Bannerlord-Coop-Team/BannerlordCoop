using System;
using System.Threading;

namespace Common.Util;

/// <summary>
/// Class allowing for atomic operations on the given object
/// Primarily used for overriding values during a Harmony patch
/// </summary>
/// <typeparam name="T">Type to allow state changing</typeparam>
public class AllowedInstance<T> : IDisposable where T : class
{
    private readonly static SemaphoreSlim _sem = new SemaphoreSlim(1);
    public T Instance { get; private set; }
    public AllowedInstance(T instance)
    {
        _sem.Wait();
        Instance = instance;
    }

    ~AllowedInstance() => Dispose();

    public void Dispose()
    {
        _sem.Release();
        Instance = null;
    }
}
