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

        if (lifetimeScope == null)
        {
            var callStack = Environment.StackTrace;
            Logger.Error("{name} was not setup properly, try using {setupFnName}\n" +
                "CallStack: {callStack}",
                nameof(ContainerProvider),
                nameof(SetContainer),
                callStack);
            return false;
        }

        return true;
    }

    public static bool TryResolve<T>(out T instance) where T : class
    {
        instance = null;

        if (TryGetContainer(out var container) == false) return false;

        if (container.TryResolve(out instance) == false)
        {
            var callStack = Environment.StackTrace;
            Logger.Error("Unable to reslove {name}\n" + 
                "CallStack: {callStack}",
                typeof(T).Name,
                callStack);
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
