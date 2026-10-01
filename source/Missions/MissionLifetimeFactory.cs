using Autofac;
using Autofac.Core;
using Autofac.Core.Lifetime;
using Common.Logging;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace Missions;

public interface IMissionLifetimeFactory : IDisposable
{
    T Create<T>(IEnumerable<Parameter> parameters) where T : CoopMissionController;
}

/// <summary>Owns active mission scopes until mission teardown or session shutdown.</summary>
public sealed class MissionLifetimeFactory : IMissionLifetimeFactory
{
    internal const string MissionTag = "coop-mission";
    private static readonly ILogger Logger = LogManager.GetLogger<MissionLifetimeFactory>();
    private readonly ILifetimeScope session;
    private readonly HashSet<MissionLifetime> active = new();
    private bool disposed;

    public MissionLifetimeFactory(ILifetimeScope session)
    {
        if (session == null) throw new ArgumentNullException(nameof(session));
        this.session = session;
        session.CurrentScopeEnding += OnSessionEnding;
    }

    public T Create<T>(IEnumerable<Parameter> parameters) where T : CoopMissionController
    {
        lock (active)
        {
            if (disposed) throw new ObjectDisposedException(nameof(MissionLifetimeFactory));
            var lifetime = new MissionLifetime(session.BeginLifetimeScope(MissionTag), Release);
            active.Add(lifetime);
            try
            {
                var controller = lifetime.Scope.ResolveKeyed<T>(MissionTag, parameters);
                lifetime.Controller = controller;
                controller.SetLifetime(lifetime);
                return controller;
            }
            catch
            {
                try { lifetime.Dispose(); }
                catch (Exception error) { Logger.Error(error, "Failed to release an incomplete mission scope"); }
                throw;
            }
        }
    }

    private void OnSessionEnding(object sender, LifetimeScopeEndingEventArgs args) => Dispose();

    private void Release(MissionLifetime lifetime)
    {
        lock (active) active.Remove(lifetime);
    }

    public void Dispose()
    {
        MissionLifetime[] lifetimes;
        lock (active)
        {
            if (disposed) return;
            disposed = true;
            session.CurrentScopeEnding -= OnSessionEnding;
            lifetimes = active.ToArray();
        }
        foreach (var lifetime in lifetimes)
        {
            try { lifetime.Dispose(); }
            catch (Exception error) { Logger.Error(error, "Failed to tear down a mission at session shutdown"); }
        }
    }
}

/// <summary>Releases one controller and its Autofac graph exactly once.</summary>
internal sealed class MissionLifetime : IDisposable
{
    private ILifetimeScope scope;
    internal ILifetimeScope Scope => scope;
    internal CoopMissionController Controller { get; set; }
    private Action<MissionLifetime> release;

    internal MissionLifetime(ILifetimeScope scope, Action<MissionLifetime> release)
    {
        this.scope = scope;
        this.release = release;
    }

    public void Dispose()
    {
        var ownedScope = Interlocked.Exchange(ref scope, null);
        if (ownedScope == null) return;
        try
        {
            Controller?.OnEndMissionInternal();
        }
        finally
        {
            Controller = null;
            try { ownedScope.Dispose(); }
            finally
            {
                release(this);
                release = null;
            }
        }
    }
}
