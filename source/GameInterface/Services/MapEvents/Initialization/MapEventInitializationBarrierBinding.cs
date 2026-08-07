using Autofac;
using Autofac.Core;
using System;
using System.Collections.Concurrent;

namespace GameInterface.Services.MapEvents.Initialization;

internal sealed class MapEventInitializationBarrierBinding : IDisposable
{
    private static readonly ConcurrentDictionary<IComponentRegistry, IMapEventInitializationBarrier> Bindings =
        new ConcurrentDictionary<IComponentRegistry, IMapEventInitializationBarrier>();

    private readonly IComponentRegistry componentRegistry;
    private readonly IMapEventInitializationBarrier barrier;
    private bool disposed;

    public MapEventInitializationBarrierBinding(
        ILifetimeScope lifetimeScope,
        IMapEventInitializationBarrier barrier)
    {
        componentRegistry = lifetimeScope.ComponentRegistry;
        this.barrier = barrier;

        if (!Bindings.TryAdd(componentRegistry, barrier))
            throw new InvalidOperationException("A map-event initialization barrier is already bound to this scope");
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        Bindings.TryRemove(componentRegistry, out _);
    }

    internal static bool TryGet(
        ILifetimeScope lifetimeScope,
        out IMapEventInitializationBarrier barrier)
    {
        barrier = null;
        return lifetimeScope != null && Bindings.TryGetValue(lifetimeScope.ComponentRegistry, out barrier);
    }
}
