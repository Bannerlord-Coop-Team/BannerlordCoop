using Autofac;
using Common.Logging;
using Serilog;
using System;
using System.Threading;

namespace GameInterface;

public class ContainerProvider
{
    private static ILogger Logger = LogManager.GetLogger<ContainerProvider>();

    private static ILifetimeScope _lifetimeScope;

    public static bool Alive { get; } = _lifetimeScope != null;

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

    public static void Clear()
    {
        _lifetimeScope = null;
    }

    public static bool TryResolve<T>(out T instance) where T : class
    {
        instance = null;

        if (TryGetContainer(out var container) == false) return false;

        try
        {
            if (container.TryResolve(out instance) == false)
            {
                Logger.Error("Unable to reslove {name}", typeof(T).Name);
                return false;
            }
        }
        catch (ObjectDisposedException)
        {
            // A disposed scope is still a non-null reference, so TryGetContainer above cannot tell it from a
            // live one, and Autofac throws rather than returning false. "The container is gone" is exactly
            // what this method reports with false, so report it instead of letting the throw escape.
            //
            // It escapes into teardown, which is the worst place for it: ConversationPartyTracker.Dispose
            // releases parties it is still holding, and a party released through DisableAi never re-enables
            // itself. An exception part-way through that loop leaves the rest frozen for the life of the save.
            instance = null;
            return false;
        }

        return true;
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
