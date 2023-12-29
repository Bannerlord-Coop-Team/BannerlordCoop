using Autofac;
using System;
using System.Threading;

namespace GameInterface;

public static class ContainerProvider
{
    private static ILifetimeScope _lifetimeScope;

    public static void SetContainer(ILifetimeScope lifetimeScope)
    {
        using(new SafeUse())
        {
            _lifetimeScope = lifetimeScope;
        }
    }

    public static bool TryGetContainer(out ILifetimeScope lifetimeScope)
    {
        lifetimeScope = _lifetimeScope;

        return lifetimeScope != null;
    }

    public static bool TryResolve<T>(out T instance) where T : class
    {
        instance = null;

        if (TryGetContainer(out var container) == false) return false;

        return container.TryResolve(out instance);
    }

    public static IDisposable UseContainerThreadSafe(ILifetimeScope lifetimeScope)
    {
        var use = new SafeUse();

        _lifetimeScope = lifetimeScope;

        return use;
    }

    class SafeUse : IDisposable
    {
        private readonly static SemaphoreSlim _sem = new SemaphoreSlim(1);

        public SafeUse()
        {
            _sem.Wait();
        }

        public void Dispose()
        {
            _sem.Release();
        }
    }
}
