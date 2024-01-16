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
    private readonly SemaphoreSlim _sem = new SemaphoreSlim(1);
    public T Instance
    {
        get => _instance;
        set {
            _sem.Wait();
            _instance = value;
        }
    }
    private T _instance;

    ~AllowedInstance() => Dispose();

    public bool IsAllowed(T instance)
    {
        return ReferenceEquals(_instance, instance);
    }

    public void Dispose()
    {
        _instance = null;
        _sem.Release();
    }
}
