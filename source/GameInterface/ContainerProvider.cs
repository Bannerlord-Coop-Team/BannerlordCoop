using Autofac;
using Autofac.Core;
using Common.Logging;
using Serilog;
using System;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Threading;

namespace GameInterface;

/// <summary>
/// Holds the active Autofac scope so static Harmony patches can resolve services from it.
/// </summary>
public class ContainerProvider
{
    private static ILogger Logger = LogManager.GetLogger<ContainerProvider>();

    private static readonly ConditionalWeakTable<ILifetimeScope, ScopeCache> _scopeCaches = new ConditionalWeakTable<ILifetimeScope, ScopeCache>();

    private static ScopeCache _active;

    public static bool Alive { get; } = _active != null;

    /// <summary>
    /// Makes <paramref name="lifetimeScope"/> the scope every later resolve uses.
    /// </summary>
    public static void SetContainer(ILifetimeScope lifetimeScope)
    {
        using(new SafeUse())
        {
            Activate(lifetimeScope);
        }
    }

    /// <summary>
    /// Returns the active scope, or false when none is set.
    /// </summary>
    public static bool TryGetContainer(out ILifetimeScope lifetimeScope)
    {
        lifetimeScope = _active?.Scope;

        return lifetimeScope != null;
    }

    /// <summary>
    /// Drops the active scope so resolves fail until a new one is set.
    /// </summary>
    public static void Clear()
    {
        _active = null;
    }

    /// <summary>
    /// Resolves <typeparamref name="T"/> from the active scope, reusing the instance when the registration is shared.
    /// </summary>
    public static bool TryResolve<T>(out T instance) where T : class
    {
        instance = null;

        var active = _active;
        if (active == null) return false;

        if (active.Resolved.TryGetValue(typeof(T), out var cached) == false) return ResolveFirst(active, out instance);

        if (ReferenceEquals(cached, ScopeCache.NotRegistered)) return false;

        if (ReferenceEquals(cached, ScopeCache.PerResolve)) return active.Scope.TryResolve(out instance);

        instance = (T)cached;
        return true;
    }

    /// <summary>
    /// Takes the active scope until the returned handle is disposed, so tests and E2E instances dont swap it under each other.
    /// </summary>
    public static IDisposable UseContainerThreadSafe(ILifetimeScope lifetimeScope)
    {
        var use = new SafeUse();

        Activate(lifetimeScope);

        return use;
    }

    /// <summary>
    /// Points <see cref="_active"/> at the cache for <paramref name="lifetimeScope"/>, reusing it when that scope was active before.
    /// </summary>
    private static void Activate(ILifetimeScope lifetimeScope)
    {
        _active = lifetimeScope == null ? null : _scopeCaches.GetValue(lifetimeScope, scope => new ScopeCache(scope));
    }

    /// <summary>
    /// Resolves <typeparamref name="T"/> for the first time in a scope and records how later resolves should be answered.
    /// </summary>
    private static bool ResolveFirst<T>(ScopeCache active, out T instance) where T : class
    {
        if (active.Scope.TryResolve(out instance) == false)
        {
            active.Resolved[typeof(T)] = ScopeCache.NotRegistered;
            Logger.Error("Unable to resolve {name}", typeof(T).Name);
            return false;
        }

        active.Resolved[typeof(T)] = IsShared<T>(active.Scope) ? instance : ScopeCache.PerResolve;
        return true;
    }

    /// <summary>
    /// Checks whether the scope hands out one instance of <typeparamref name="T"/>, so caching it cant change what callers get.
    /// </summary>
    private static bool IsShared<T>(ILifetimeScope scope)
    {
        if (scope.ComponentRegistry.TryGetRegistration(new TypedService(typeof(T)), out var registration) == false) return false;

        return registration.Sharing == InstanceSharing.Shared;
    }

    /// <summary>
    /// Pairs a scope with what was already resolved from it, so a scope swap can never read another scope's instances.
    /// </summary>
    private sealed class ScopeCache
    {
        public static readonly object NotRegistered = new object();
        public static readonly object PerResolve = new object();

        public ScopeCache(ILifetimeScope scope)
        {
            Scope = scope;
        }

        public ILifetimeScope Scope { get; }
        public ConcurrentDictionary<Type, object> Resolved { get; } = new ConcurrentDictionary<Type, object>();
    }

    /// <summary>
    /// Serializes who owns the active scope while the handle is held.
    /// </summary>
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
