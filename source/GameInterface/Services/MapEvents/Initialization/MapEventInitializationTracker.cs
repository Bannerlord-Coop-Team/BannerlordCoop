using GameInterface.Services;
using GameInterface.Services.ObjectManager;
using HarmonyLib;
using SandBox.GauntletUI.Map;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.Library;

namespace GameInterface.Services.MapEvents.Initialization;

/// <summary>
/// Tracks the objects which belong to a MapEvent's initial object graph. While an object is pending,
/// its ordinary lifetime create and AutoSync messages are suppressed in favour of the aggregate
/// MapEvent initialization message.
/// </summary>
public interface IMapEventInitializationTracker : IGameAbstraction
{
    /// <summary>
    /// Opens the scope in which a MapEvent constructor creates its visual and troop-upgrade tracker.
    /// </summary>
    IDisposable BeginMapEventConstruction();

    /// <summary>
    /// Opens a scope for no-argument child objects which vanilla creates while mutating an already-known
    /// aggregate root. This covers children such as the replacement troop-upgrade tracker created when a
    /// party is attached, both during initialization and after the graph has committed.
    /// </summary>
    IDisposable BeginGraphChildConstruction(MapEvent mapEvent);

    /// <summary>
    /// Marks <paramref name="mapEvent"/> as being built by <c>MapEvent.Initialize</c> and the
    /// subsequent <c>MapEventManager.OnMapEventCreated</c> work.
    /// </summary>
    void BeginBuild(MapEvent mapEvent);

    /// <summary>Returns whether <paramref name="mapEvent"/> is still executing vanilla Initialize.</summary>
    bool IsInitializing(MapEvent mapEvent);

    /// <summary>Closes the vanilla Initialize phase without committing or aborting the transaction.</summary>
    void EndInitialization(MapEvent mapEvent);

    /// <summary>
    /// Opens the scope in which a MapEventParty creates its dummy TroopRosters. The returned scope is a
    /// no-op when <paramref name="party"/> does not belong to an aggregate-owned MapEvent graph.
    /// </summary>
    IDisposable BeginMapEventPartyConstruction(PartyBase party);

    /// <summary>
    /// Determines whether a newly registered object belongs to the pending MapEvent graph and, when it
    /// does, records it by reference. Constructor arguments are used to establish graph ownership. Late
    /// children of a committed graph are recorded for aggregate teardown but return <see langword="false"/>
    /// so their ordinary lifetime packets continue.
    /// </summary>
    bool TryDefer(object instance, object[] constructorArguments);

    /// <summary>
    /// Returns whether <paramref name="instance"/> is waiting for aggregate MapEvent replication.
    /// </summary>
    bool IsPending(object instance);

    /// <summary>
    /// Returns whether <paramref name="mapEvent"/> is inside its aggregate build window.
    /// </summary>
    bool IsBuilding(MapEvent mapEvent);

    /// <summary>Returns whether the aggregate was already queued during an in-Initialize finalization.</summary>
    bool IsPublished(MapEvent mapEvent);

    /// <summary>Records the graph snapshot which was queued before an in-Initialize destroy stream.</summary>
    void MarkPublished(MapEvent mapEvent, IEnumerable<object> graphObjects);

    /// <summary>Commits a graph which was published before vanilla Initialize returned.</summary>
    void CompletePublishedBuild(MapEvent mapEvent);

    /// <summary>
    /// Completes a fully-enumerated graph after its aggregate message has been queued. Every pending
    /// object owned by the root is released, including constructor-created objects which were replaced
    /// before the final graph was captured.
    /// </summary>
    void CompleteBuild(MapEvent mapEvent, IEnumerable<object> graphObjects);

    /// <summary>
    /// Releases every pending object owned by <paramref name="mapEvent"/> after a failed build.
    /// </summary>
    void AbortBuild(MapEvent mapEvent);

    /// <summary>Records a client-hydrated graph for symmetric aggregate teardown.</summary>
    void RegisterCommittedGraph(MapEvent mapEvent, IEnumerable<object> graphObjects);

    /// <summary>
    /// Extends an already-committed graph with late objects which were hydrated through ordinary lifetime
    /// packets. Repeated references are ignored. An untracked root is left unchanged.
    /// </summary>
    void ExtendCommittedGraph(MapEvent mapEvent, IEnumerable<object> graphObjects);

    /// <summary>Atomically removes every registered object owned by a committed aggregate graph.</summary>
    void DestroyGraph(MapEvent mapEvent);
}

internal sealed class MapEventInitializationTracker : IMapEventInitializationTracker
{
    private static readonly System.Reflection.FieldInfo MapEventsField =
        AccessTools.Field(typeof(MapEventManager), "_mapEvents");

    private readonly IObjectManager objectManager;
    private readonly object gate = new object();

    private readonly HashSet<object> pending =
        new HashSet<object>(ReferenceComparer<object>.Instance);

    private readonly Dictionary<object, MapEvent> pendingRoots =
        new Dictionary<object, MapEvent>(ReferenceComparer<object>.Instance);

    private readonly HashSet<MapEvent> building =
        new HashSet<MapEvent>(ReferenceComparer<MapEvent>.Instance);

    private readonly HashSet<MapEvent> initializing =
        new HashSet<MapEvent>(ReferenceComparer<MapEvent>.Instance);

    private readonly Dictionary<MapEvent, List<object>> publishedGraphs =
        new Dictionary<MapEvent, List<object>>(ReferenceComparer<MapEvent>.Instance);

    private readonly Dictionary<MapEvent, List<object>> committedGraphs =
        new Dictionary<MapEvent, List<object>>(ReferenceComparer<MapEvent>.Instance);

    // Constructor patches are synchronous, but more than one game lifetime can exist in-process and
    // test environments can construct on different threads. Keep the nesting state per thread rather
    // than using one global depth counter.
    private readonly Dictionary<int, Stack<MapEventConstructionContext>> mapEventConstructionScopes =
        new Dictionary<int, Stack<MapEventConstructionContext>>();

    private readonly Dictionary<int, Stack<MapEventPartyConstructionContext>> mapEventPartyConstructionScopes =
        new Dictionary<int, Stack<MapEventPartyConstructionContext>>();

    // Harmony does not guarantee the relative order of independently-owned prefixes. Remember the root
    // seen by the lifetime prefix so BeginMapEventConstruction can still associate it when that prefix
    // happened first; the normal ordering instead fills the active context from TryDefer(MapEvent).
    private readonly Dictionary<int, MapEvent> latestConstructedMapEvent =
        new Dictionary<int, MapEvent>();

    public MapEventInitializationTracker(IObjectManager objectManager)
    {
        this.objectManager = objectManager;
    }

    public IDisposable BeginMapEventConstruction()
    {
        int threadId = Thread.CurrentThread.ManagedThreadId;
        MapEventConstructionContext context;

        lock (gate)
        {
            latestConstructedMapEvent.TryGetValue(threadId, out var root);
            context = new MapEventConstructionContext(root);

            if (!mapEventConstructionScopes.TryGetValue(threadId, out var scopes))
            {
                scopes = new Stack<MapEventConstructionContext>();
                mapEventConstructionScopes.Add(threadId, scopes);
            }

            scopes.Push(context);
        }

        return new CallbackScope(() => EndMapEventConstruction(threadId, context));
    }

    public IDisposable BeginGraphChildConstruction(MapEvent mapEvent)
    {
        if (mapEvent == null) return NoopScope.Instance;

        int threadId = Thread.CurrentThread.ManagedThreadId;
        MapEventConstructionContext context;

        lock (gate)
        {
            if (!IsPendingBuildingOrCommittedLocked(mapEvent)) return NoopScope.Instance;

            context = new MapEventConstructionContext(mapEvent);
            if (!mapEventConstructionScopes.TryGetValue(threadId, out var scopes))
            {
                scopes = new Stack<MapEventConstructionContext>();
                mapEventConstructionScopes.Add(threadId, scopes);
            }

            scopes.Push(context);
        }

        return new CallbackScope(() => EndMapEventConstruction(threadId, context));
    }

    public void BeginBuild(MapEvent mapEvent)
    {
        if (mapEvent == null) throw new ArgumentNullException(nameof(mapEvent));

        lock (gate)
        {
            MarkPendingLocked(mapEvent, mapEvent);
            building.Add(mapEvent);
            initializing.Add(mapEvent);
        }
    }

    public bool IsInitializing(MapEvent mapEvent)
    {
        if (mapEvent == null) return false;

        lock (gate)
        {
            return initializing.Contains(mapEvent);
        }
    }

    public void EndInitialization(MapEvent mapEvent)
    {
        if (mapEvent == null) return;

        lock (gate)
        {
            initializing.Remove(mapEvent);
        }
    }

    public IDisposable BeginMapEventPartyConstruction(PartyBase party)
    {
        var root = party?.MapEventSide?.MapEvent;
        if (root == null) return NoopScope.Instance;

        int threadId = Thread.CurrentThread.ManagedThreadId;
        MapEventPartyConstructionContext context;

        lock (gate)
        {
            // Later reinforcements use ordinary lifetime replication, but remain owned by the aggregate
            // root so destroying the MapEvent also tears down their nested casualty rosters.
            if (!IsPendingBuildingOrCommittedLocked(root)) return NoopScope.Instance;

            context = new MapEventPartyConstructionContext(root);
            if (!mapEventPartyConstructionScopes.TryGetValue(threadId, out var scopes))
            {
                scopes = new Stack<MapEventPartyConstructionContext>();
                mapEventPartyConstructionScopes.Add(threadId, scopes);
            }

            scopes.Push(context);
        }

        return new CallbackScope(() => EndMapEventPartyConstruction(threadId, context));
    }

    public bool TryDefer(object instance, object[] constructorArguments)
    {
        if (instance == null) return false;

        int threadId = Thread.CurrentThread.ManagedThreadId;

        lock (gate)
        {
            if (pending.Contains(instance)) return true;

            MapEvent root;

            if (instance is MapEvent mapEvent)
            {
                root = mapEvent;
                latestConstructedMapEvent[threadId] = mapEvent;

                var construction = GetCurrentMapEventConstructionLocked(threadId);
                if (construction != null)
                {
                    construction.Root = mapEvent;
                }
            }
            else if (instance is MapEventComponent || instance is GauntletMapEventVisual)
            {
                root = FindPendingRootArgumentLocked(constructorArguments);
            }
            else if (instance is MapEventSide)
            {
                root = FindPendingRootArgumentLocked(constructorArguments);
            }
            else if (instance is MapEventParty)
            {
                root = FindTrackedPartyRootLocked(constructorArguments);
            }
            else if (instance is TroopUpgradeTracker)
            {
                root = GetCurrentMapEventConstructionLocked(threadId)?.Root;
            }
            else if (instance is TroopRoster)
            {
                root = GetCurrentMapEventPartyConstructionLocked(threadId)?.Root;
            }
            else
            {
                return false;
            }

            if (root == null) return false;

            if (IsLateCommittedChild(instance) &&
                TryAppendCommittedGraphObjectLocked(root, instance))
            {
                // The object still needs its ordinary create packet. Recording it here only extends the
                // root's teardown ownership to cover objects added after the aggregate was published.
                return false;
            }

            MarkPendingLocked(instance, root);
            return true;
        }
    }

    public bool IsPending(object instance)
    {
        if (instance == null) return false;

        lock (gate)
        {
            return pending.Contains(instance);
        }
    }

    public bool IsBuilding(MapEvent mapEvent)
    {
        if (mapEvent == null) return false;

        lock (gate)
        {
            return building.Contains(mapEvent);
        }
    }

    public bool IsPublished(MapEvent mapEvent)
    {
        if (mapEvent == null) return false;

        lock (gate)
        {
            return publishedGraphs.ContainsKey(mapEvent);
        }
    }

    public void MarkPublished(MapEvent mapEvent, IEnumerable<object> graphObjects)
    {
        if (mapEvent == null) throw new ArgumentNullException(nameof(mapEvent));

        var graph = MaterializeGraph(graphObjects);
        lock (gate)
        {
            if (!building.Contains(mapEvent))
                throw new InvalidOperationException("Cannot publish a MapEvent outside its initialization transaction");

            publishedGraphs[mapEvent] = graph;
        }
    }

    public void CompletePublishedBuild(MapEvent mapEvent)
    {
        if (mapEvent == null) throw new ArgumentNullException(nameof(mapEvent));

        List<object> graph;
        lock (gate)
        {
            if (!publishedGraphs.TryGetValue(mapEvent, out graph)) return;
            graph = new List<object>(graph);
        }

        CompleteBuild(mapEvent, graph);
    }

    public void CompleteBuild(MapEvent mapEvent, IEnumerable<object> graphObjects)
    {
        if (mapEvent == null) throw new ArgumentNullException(nameof(mapEvent));
        var graph = MaterializeGraph(graphObjects);
        List<object> owned;
        bool finalized = mapEvent.IsFinalized;

        lock (gate)
        {
            owned = GetOwnedObjectsLocked(mapEvent);
            ReleaseOwnedObjectsLocked(mapEvent);
            building.Remove(mapEvent);
            initializing.Remove(mapEvent);
            publishedGraphs.Remove(mapEvent);

            if (finalized)
            {
                committedGraphs.Remove(mapEvent);
            }
            else
            {
                committedGraphs[mapEvent] = graph;
            }
        }

        var liveObjects = new HashSet<object>(graph, ReferenceComparer<object>.Instance);
        var registrationsToRemove = new List<object>();
        foreach (var instance in owned)
        {
            if (finalized || !liveObjects.Contains(instance))
                registrationsToRemove.Add(instance);
        }

        if (finalized)
            registrationsToRemove.AddRange(graph);

        objectManager.RemoveExistingBatch(registrationsToRemove);
    }

    public void AbortBuild(MapEvent mapEvent)
    {
        if (mapEvent == null) throw new ArgumentNullException(nameof(mapEvent));

        List<object> owned;

        lock (gate)
        {
            owned = GetOwnedObjectsLocked(mapEvent);
            if (publishedGraphs.TryGetValue(mapEvent, out var published))
                owned.AddRange(published);
            if (committedGraphs.TryGetValue(mapEvent, out var committed))
                owned.AddRange(committed);
        }

        EndOwnedVisuals(mapEvent, owned);
        RollBackExternalEdges(mapEvent, owned);
        RemoveFromManager(mapEvent);

        lock (gate)
        {
            ReleaseOwnedObjectsLocked(mapEvent);
            building.Remove(mapEvent);
            initializing.Remove(mapEvent);
            publishedGraphs.Remove(mapEvent);
            committedGraphs.Remove(mapEvent);
        }

        objectManager.RemoveExistingBatch(owned);
    }

    private void EndOwnedVisuals(MapEvent mapEvent, IEnumerable<object> owned)
    {
        var visuals = new List<IMapEventVisual>();
        var seen = new HashSet<IMapEventVisual>(ReferenceComparer<IMapEventVisual>.Instance);
        foreach (var instance in owned)
        {
            if (instance is IMapEventVisual visual && seen.Add(visual))
                visuals.Add(visual);
        }

        if (mapEvent.MapEventVisual is IMapEventVisual rootVisual && seen.Add(rootVisual))
            visuals.Add(rootVisual);

        // Remove registry visibility first. OnMapEventEnd still performs the vanilla creator/UI and
        // sound cleanup, while AutoRegistryHandler recognizes the still-pending unregistered instance
        // and suppresses the destroy packet for a visual whose create packet was never published.
        objectManager.RemoveExistingBatch(visuals);
        foreach (var visual in visuals)
        {
            try
            {
                visual.OnMapEventEnd();
            }
            catch
            {
                // Rollback must continue even when a third-party visual implementation fails cleanup.
            }
        }
    }

    public void RegisterCommittedGraph(MapEvent mapEvent, IEnumerable<object> graphObjects)
    {
        if (mapEvent == null) throw new ArgumentNullException(nameof(mapEvent));
        var graph = MaterializeGraph(graphObjects);

        lock (gate)
        {
            if (committedGraphs.TryGetValue(mapEvent, out var existing))
                AppendUniqueGraphObjects(existing, graph);
            else
                committedGraphs[mapEvent] = graph;
        }
    }

    public void ExtendCommittedGraph(MapEvent mapEvent, IEnumerable<object> graphObjects)
    {
        if (mapEvent == null) throw new ArgumentNullException(nameof(mapEvent));

        lock (gate)
        {
            if (!committedGraphs.TryGetValue(mapEvent, out var graph)) return;
            AppendUniqueGraphObjects(graph, graphObjects);
        }
    }

    public void DestroyGraph(MapEvent mapEvent)
    {
        if (mapEvent == null) return;

        List<object> graph;
        lock (gate)
        {
            if (!committedGraphs.TryGetValue(mapEvent, out graph)) return;
            committedGraphs.Remove(mapEvent);
        }

        // Ordinary reinforcement packets can update the live graph before their explicit ownership
        // extension runs, and vanilla can replace the tracker as parties are attached. Union the current
        // reachable graph with the recorded snapshot so teardown covers both cases.
        AppendUniqueGraphObjects(graph, MapEventGraph.Enumerate(mapEvent));
        objectManager.RemoveExistingBatch(graph);
    }

    private MapEvent FindPendingRootArgumentLocked(object[] constructorArguments)
    {
        if (constructorArguments == null) return null;

        foreach (var argument in constructorArguments)
        {
            if (argument is MapEvent root && IsPendingOrBuildingLocked(root))
            {
                return root;
            }
        }

        return null;
    }

    private MapEvent FindTrackedPartyRootLocked(object[] constructorArguments)
    {
        if (constructorArguments == null) return null;

        foreach (var argument in constructorArguments)
        {
            if (argument is not PartyBase party) continue;

            var root = party.MapEventSide?.MapEvent;
            if (root != null && IsPendingBuildingOrCommittedLocked(root))
            {
                return root;
            }
        }

        return null;
    }

    private bool IsPendingOrBuildingLocked(MapEvent mapEvent) =>
        pending.Contains(mapEvent) || building.Contains(mapEvent);

    private bool IsPendingBuildingOrCommittedLocked(MapEvent mapEvent) =>
        IsPendingOrBuildingLocked(mapEvent) || committedGraphs.ContainsKey(mapEvent);

    private static bool IsLateCommittedChild(object instance) =>
        instance is MapEventParty ||
        instance is TroopRoster ||
        instance is TroopUpgradeTracker;

    private bool TryAppendCommittedGraphObjectLocked(MapEvent mapEvent, object instance)
    {
        if (!committedGraphs.TryGetValue(mapEvent, out var graph)) return false;

        foreach (var existing in graph)
        {
            if (ReferenceEquals(existing, instance)) return true;
        }

        graph.Add(instance);
        return true;
    }

    private void MarkPendingLocked(object instance, MapEvent root)
    {
        pending.Add(instance);
        pendingRoots[instance] = root;
    }

    private void ReleaseOwnedObjectsLocked(MapEvent mapEvent)
    {
        var toRelease = new List<object>();
        foreach (var pair in pendingRoots)
        {
            if (ReferenceEquals(pair.Value, mapEvent))
            {
                toRelease.Add(pair.Key);
            }
        }

        foreach (var instance in toRelease)
        {
            pending.Remove(instance);
            pendingRoots.Remove(instance);
        }
    }

    private List<object> GetOwnedObjectsLocked(MapEvent mapEvent)
    {
        var owned = new List<object>();
        foreach (var pair in pendingRoots)
        {
            if (ReferenceEquals(pair.Value, mapEvent))
                owned.Add(pair.Key);
        }

        return owned;
    }

    private static List<object> MaterializeGraph(IEnumerable<object> graphObjects)
    {
        if (graphObjects == null) throw new ArgumentNullException(nameof(graphObjects));

        var graph = new List<object>();
        AppendUniqueGraphObjects(graph, graphObjects);
        return graph;
    }

    private static void AppendUniqueGraphObjects(List<object> graph, IEnumerable<object> graphObjects)
    {
        if (graphObjects == null) throw new ArgumentNullException(nameof(graphObjects));

        var seen = new HashSet<object>(ReferenceComparer<object>.Instance);
        foreach (var instance in graph)
        {
            if (instance != null)
                seen.Add(instance);
        }

        foreach (var instance in graphObjects)
        {
            if (instance != null && seen.Add(instance))
                graph.Add(instance);
        }
    }

    private static void RollBackExternalEdges(MapEvent mapEvent, IEnumerable<object> owned)
    {
        foreach (var instance in owned)
        {
            if (instance is not MapEventParty mapEventParty) continue;

            var party = mapEventParty.Party;
            if (party?._mapEventSide?.MapEvent != mapEvent) continue;

            if (party.MobileParty != null)
                party.MobileParty.EventPositionAdder = Vec2.Zero;

            party._mapEventSide = null;
        }
    }

    private static void RemoveFromManager(MapEvent mapEvent)
    {
        var manager = Campaign.Current?.MapEventManager;
        if (manager == null) return;

        if (MapEventsField.GetValue(manager) is MBList<MapEvent> mapEvents)
            mapEvents.Remove(mapEvent);
    }

    private MapEventConstructionContext GetCurrentMapEventConstructionLocked(int threadId)
    {
        if (!mapEventConstructionScopes.TryGetValue(threadId, out var scopes)) return null;

        DiscardInactiveScopes(scopes);
        return scopes.Count == 0 ? null : scopes.Peek();
    }

    private MapEventPartyConstructionContext GetCurrentMapEventPartyConstructionLocked(int threadId)
    {
        if (!mapEventPartyConstructionScopes.TryGetValue(threadId, out var scopes)) return null;

        DiscardInactiveScopes(scopes);
        return scopes.Count == 0 ? null : scopes.Peek();
    }

    private void EndMapEventConstruction(int threadId, MapEventConstructionContext context)
    {
        lock (gate)
        {
            context.Active = false;

            if (mapEventConstructionScopes.TryGetValue(threadId, out var scopes))
            {
                DiscardInactiveScopes(scopes);
                if (scopes.Count == 0) mapEventConstructionScopes.Remove(threadId);
            }

            if (latestConstructedMapEvent.TryGetValue(threadId, out var latest) &&
                ReferenceEquals(latest, context.Root))
            {
                latestConstructedMapEvent.Remove(threadId);
            }
        }
    }

    private void EndMapEventPartyConstruction(int threadId, MapEventPartyConstructionContext context)
    {
        lock (gate)
        {
            context.Active = false;

            if (mapEventPartyConstructionScopes.TryGetValue(threadId, out var scopes))
            {
                DiscardInactiveScopes(scopes);
                if (scopes.Count == 0) mapEventPartyConstructionScopes.Remove(threadId);
            }
        }
    }

    private static void DiscardInactiveScopes<TContext>(Stack<TContext> scopes)
        where TContext : ConstructionContext
    {
        while (scopes.Count > 0 && !scopes.Peek().Active)
        {
            scopes.Pop();
        }
    }

    private abstract class ConstructionContext
    {
        public bool Active { get; set; } = true;
    }

    private sealed class MapEventConstructionContext : ConstructionContext
    {
        public MapEventConstructionContext(MapEvent root)
        {
            Root = root;
        }

        public MapEvent Root { get; set; }
    }

    private sealed class MapEventPartyConstructionContext : ConstructionContext
    {
        public MapEventPartyConstructionContext(MapEvent root)
        {
            Root = root;
        }

        public MapEvent Root { get; }
    }

    private sealed class CallbackScope : IDisposable
    {
        private Action onDispose;

        public CallbackScope(Action onDispose)
        {
            this.onDispose = onDispose;
        }

        public void Dispose()
        {
            Interlocked.Exchange(ref onDispose, null)?.Invoke();
        }
    }

    private sealed class NoopScope : IDisposable
    {
        public static readonly NoopScope Instance = new NoopScope();

        private NoopScope()
        {
        }

        public void Dispose()
        {
        }
    }

    private sealed class ReferenceComparer<T> : IEqualityComparer<T>
        where T : class
    {
        public static readonly ReferenceComparer<T> Instance = new ReferenceComparer<T>();

        private ReferenceComparer()
        {
        }

        public bool Equals(T x, T y) => ReferenceEquals(x, y);

        public int GetHashCode(T obj) => RuntimeHelpers.GetHashCode(obj);
    }
}
