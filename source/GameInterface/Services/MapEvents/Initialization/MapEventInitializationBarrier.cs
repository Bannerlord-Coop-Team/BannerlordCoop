using Common;
using Common.Logging;
using Common.Network;
using Common.Util;
using GameInterface.Services.GuantletMapEventVisuals;
using GameInterface.Services.MapEvents.Messages.Start;
using GameInterface.Services.ObjectManager;
using HarmonyLib;
using SandBox.GauntletUI.Map;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Library;

namespace GameInterface.Services.MapEvents.Initialization;

/// <summary>
/// Keeps a streamed MapEvent private until the reliable-ordered initialization stream reaches its
/// final packet. The packets still describe their natural objects; this service only owns the commit.
/// </summary>
public interface IMapEventInitializationBarrier : IGameAbstraction
{
    bool IsPending(MapEvent mapEvent);
    bool IsPartyPending(PartyBase party);
    void BeginServer(MapEvent mapEvent);
    void AnnounceServerParty(MapEvent mapEvent, PartyBase party);
    void CancelServerParty(MapEvent mapEvent, PartyBase party);
    void CommitServer(MapEvent mapEvent);
    void CommitTerminalServer(MapEvent mapEvent);
    void AbortServer(MapEvent mapEvent);
    void RegisterClient(MapEvent mapEvent);
    void LockClientParty(MapEvent mapEvent, PartyBase party);
    void UnlockClientParty(MapEvent mapEvent, PartyBase party);
    void CommitClient(MapEvent mapEvent, bool isTerminal);
    void AttachClient(MapEventSide side, MapEventParty party, Action afterCommit = null);
    void DetachClient(MapEventSide side, MapEventParty party);
    void TrackParty(MapEvent mapEvent, MapEventParty party);
    void AfterClientCommit(MapEvent mapEvent, Action action);
    void DeferVisual(GauntletMapEventVisual visual, CampaignVec2 position);
    bool EndVisual(GauntletMapEventVisual visual, bool force = false);
    void AdoptExisting(MapEvent mapEvent);
    void DestroyGraph(MapEvent mapEvent);
}

internal sealed class MapEventInitializationBarrier : IMapEventInitializationBarrier, IDisposable
{
    private static readonly ILogger Logger = LogManager.GetLogger<MapEventInitializationBarrier>();
    private readonly INetwork network;
    private readonly IObjectManager objectManager;
    private readonly Dictionary<MapEvent, State> states = new Dictionary<MapEvent, State>();
    private volatile bool disposed;

    public MapEventInitializationBarrier(
        INetwork network,
        IObjectManager objectManager)
    {
        this.network = network;
        this.objectManager = objectManager;
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        foreach (var state in states.Values.ToArray())
            ReleaseClientParties(state);
        states.Clear();
    }

    public bool IsPending(MapEvent mapEvent) => mapEvent != null &&
        states.TryGetValue(mapEvent, out var state) && !state.Committed;

    public bool IsPartyPending(PartyBase party) => PendingMapEventPartyLock.IsLocked(party);

    public void BeginServer(MapEvent mapEvent)
    {
        if (disposed) return;
        if (mapEvent != null) states[mapEvent] = new State(new[] { mapEvent });
    }

    public void AnnounceServerParty(MapEvent mapEvent, PartyBase party)
    {
        if (mapEvent == null || party == null) return;
        if (!states.TryGetValue(mapEvent, out var state) || state.Terminal) return;
        if (!state.AnnouncedParties.Add(party)) return;
        if (!objectManager.TryGetIdWithLogging(mapEvent, out var mapEventId) ||
            !objectManager.TryGetIdWithLogging(party, out var partyId))
        {
            state.AnnouncedParties.Remove(party);
            return;
        }

        network.SendAll(new NetworkMapEventPartyPending(mapEventId, partyId));
    }

    public void CancelServerParty(MapEvent mapEvent, PartyBase party)
    {
        if (mapEvent == null || party == null) return;
        if (!states.TryGetValue(mapEvent, out var state) || state.Terminal) return;

        state.AnnouncedParties.Remove(party);
        if (!objectManager.TryGetIdWithLogging(mapEvent, out var mapEventId) ||
            !objectManager.TryGetIdWithLogging(party, out var partyId))
            return;

        network.SendAll(new NetworkMapEventPartyPending(mapEventId, partyId, isCancellation: true));
    }

    public void CommitServer(MapEvent mapEvent) => PublishServer(mapEvent, false);

    public void CommitTerminalServer(MapEvent mapEvent) => PublishServer(mapEvent, true);

    public void AbortServer(MapEvent mapEvent)
    {
        if (mapEvent == null) return;
        PublishServer(mapEvent, true);

        if (mapEvent.MapEventVisual is GauntletMapEventVisual visual) EndVisual(visual, force: true);

        DestroyGraph(mapEvent);
    }

    private void PublishServer(MapEvent mapEvent, bool terminal)
    {
        if (mapEvent == null) return;

        if (!states.TryGetValue(mapEvent, out var state) || state.Committed || state.Terminal) return;

        if (!objectManager.TryGetIdWithLogging(mapEvent, out var id))
            throw new InvalidOperationException("Cannot commit an unregistered MapEvent");

        objectManager.TryGetId(MapEventGraph.GetTracker(mapEvent), out var trackerId);

        // ReliableOrdered makes this the queue barrier for every create and apply sent before it. The
        // tracker id is included because its constructor property initializer bypasses the setter patch.
        network.SendAll(new NetworkMapEventInitialized(id, terminal, trackerId));

        AddOwned(state, MapEventGraph.Enumerate(mapEvent));
        state.Committed = !terminal;
        state.Terminal = terminal;
        state.VisualPublished = !terminal && mapEvent.MapEventVisual != null;
    }

    public void RegisterClient(MapEvent mapEvent)
    {
        if (disposed) return;
        if (mapEvent == null) return;
        if (states.TryGetValue(mapEvent, out var state))
        {
            state.Owned.Add(mapEvent);
            return;
        }

        states[mapEvent] = new State(new[] { mapEvent });
    }

    public void LockClientParty(MapEvent mapEvent, PartyBase party)
    {
        if (disposed) return;
        if (mapEvent == null || party == null) return;
        if (!states.TryGetValue(mapEvent, out var state))
        {
            state = new State(new[] { mapEvent });
            states.Add(mapEvent, state);
        }
        if (state.Terminal) return;

        TrackClientPartyLock(state, party);
    }

    public void UnlockClientParty(MapEvent mapEvent, PartyBase party)
    {
        if (mapEvent == null || party == null) return;
        if (states.TryGetValue(mapEvent, out var state))
            ReleaseClientParty(state, party);
    }

    public void CommitClient(MapEvent mapEvent, bool isTerminal)
    {
        if (disposed) return;
        if (mapEvent == null) return;

        if (!states.TryGetValue(mapEvent, out var state))
        {
            state = new State(new[] { mapEvent });
            states.Add(mapEvent, state);
        }

        AddOwned(state, MapEventGraph.Enumerate(mapEvent));
        if (state.Committed || state.Terminal) return;
        state.Terminal = isTerminal;

        if (isTerminal)
        {
            DestroyGraph(mapEvent);
            return;
        }

        if (!IsComplete(mapEvent, state))
        {
            state.Terminal = true;
            Logger.Error("MapEvent {MapEventId} reached its initialization barrier with an incomplete graph", mapEvent.StringId);
            DestroyGraph(mapEvent);
            return;
        }

        PublishClientGraph(mapEvent, state);
    }

    private void PublishClientGraph(MapEvent mapEvent, State state)
    {
        try
        {
            using (new AllowedThread())
            {
                foreach (var edge in state.DeferredEdges)
                {
                    if (edge.Party?.Party != null)
                        edge.Party.Party._mapEventSide = edge.Side;
                }
            }

            ReleaseClientParties(state);

            if (state.Visual != null)
            {
                state.VisualPublished = true;
                PublishVisual(state.Visual, state.VisualPosition);
            }

            foreach (var callback in state.AfterCommit)
                callback();

            var manager = Campaign.Current?.MapEventManager;
            if (manager != null && !manager.MapEvents.Contains(mapEvent))
            {
                using (new AllowedThread())
                {
                    manager.OnMapEventCreated(mapEvent);
                }
            }

            state.Committed = true;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Failed to publish MapEvent {MapEventId}; rolling back its graph", mapEvent.StringId);
            state.Terminal = true;
            DestroyGraph(mapEvent);
        }
    }

    public void AttachClient(MapEventSide side, MapEventParty party, Action afterCommit = null)
    {
        if (disposed) return;
        if (side == null || party == null) return;

        var root = side.MapEvent;
        if (root == null) return;
        states.TryGetValue(root, out var state);
        if (state?.Terminal == true) return;

        if (state != null)
            TrackClientPartyLock(state, party.Party);

        AddPartyToSide(side, party);
        if (TryDeferAttachment(root, side, party, afterCommit)) return;

        PublishPartyAttachment(side, party);
        if (state != null)
            ReleaseClientParty(state, party.Party);
        afterCommit?.Invoke();
    }

    private static void AddPartyToSide(MapEventSide side, MapEventParty party)
    {
        using (new AllowedThread())
        {
            if (!side._battleParties.Contains(party))
                side._battleParties.Add(party);
        }
    }

    private bool TryDeferAttachment(
        MapEvent root,
        MapEventSide side,
        MapEventParty party,
        Action afterCommit)
    {
        if (!states.TryGetValue(root, out var state)) return false;

        AddOwned(state, MapEventGraph.EnumerateParty(party));
        AddOwned(state, new object[] { MapEventGraph.GetTracker(root) });
        if (state.Committed || state.Terminal) return false;

        TrackClientPartyLock(state, party.Party);

        if (!state.DeferredEdges.Any(edge => ReferenceEquals(edge.Party, party)))
            state.DeferredEdges.Add((side, party));
        if (afterCommit != null) state.AfterCommit.Add(afterCommit);
        return true;
    }

    private void PublishPartyAttachment(
        MapEventSide side,
        MapEventParty party)
    {
        using (new AllowedThread())
        {
            if (party.Party != null)
                party.Party._mapEventSide = side;
        }

    }

    public void DetachClient(MapEventSide side, MapEventParty party)
    {
        if (side == null || party == null) return;

        if (side.MapEvent != null && states.TryGetValue(side.MapEvent, out var state))
        {
            state.DeferredEdges.RemoveAll(edge => ReferenceEquals(edge.Party, party));
            if (!state.DeferredEdges.Any(edge => ReferenceEquals(edge.Party?.Party, party.Party)))
                ReleaseClientParty(state, party.Party);
        }

        using (new AllowedThread())
        {
            side._battleParties.Remove(party);
            if (party.Party?._mapEventSide == side)
                party.Party._mapEventSide = null;
        }
    }

    public void TrackParty(MapEvent mapEvent, MapEventParty party)
    {
        if (mapEvent == null || party == null) return;
        if (!states.TryGetValue(mapEvent, out var state)) return;
        state.AnnouncedParties.Remove(party.Party);
        AddOwned(state, MapEventGraph.EnumerateParty(party));
        AddOwned(state, new object[] { MapEventGraph.GetTracker(mapEvent) });
    }

    public void AfterClientCommit(MapEvent mapEvent, Action action)
    {
        if (disposed) return;
        if (mapEvent == null || action == null) return;
        if (states.TryGetValue(mapEvent, out var state))
        {
            if (state.Terminal) return;
            if (!state.Committed)
            {
                state.AfterCommit.Add(action);
                return;
            }
        }

        action();
    }

    public void DeferVisual(GauntletMapEventVisual visual, CampaignVec2 position)
    {
        if (disposed) return;
        var root = visual?.MapEvent;
        if (root == null)
        {
            Logger.Warning("Ignoring a MapEvent visual initialization whose root has not synchronized");
            return;
        }

        if (states.TryGetValue(root, out var state))
        {
            AddOwned(state, new object[] { visual });
            if (state.Terminal) return;
            if (!state.Committed)
            {
                state.Visual = visual;
                state.VisualPosition = position;
                return;
            }
        }

        if (state != null)
        {
            state.Visual = visual;
            state.VisualPublished = true;
        }

        try
        {
            PublishVisual(visual, position);
        }
        catch
        {
            try { EndVisual(visual, force: true); }
            catch (Exception ex) { Logger.Warning(ex, "Failed to roll back MapEvent visual publication"); }
            throw;
        }
    }

    public void AdoptExisting(MapEvent mapEvent)
    {
        if (disposed) return;
        if (mapEvent == null) return;

        if (states.TryGetValue(mapEvent, out var existing))
        {
            AddOwned(existing, MapEventGraph.Enumerate(mapEvent));
            if (existing.Committed && mapEvent.MapEventVisual != null)
                existing.VisualPublished = true;
            return;
        }

        states[mapEvent] = new State(MapEventGraph.Enumerate(mapEvent))
        {
            Committed = true,
            VisualPublished = mapEvent.MapEventVisual != null
        };
    }

    public bool EndVisual(GauntletMapEventVisual visual, bool force = false)
    {
        if (visual == null) return false;
        states.TryGetValue(visual.MapEvent, out var state);
        var creator = Campaign.Current?.VisualCreator?.MapEventVisualCreator as GauntletMapEventVisualCreator;
        if (!force && state?.VisualPublished != true &&
            creator?.GetCurrentEvents().Contains(visual) != true) return false;
        if (state != null) state.VisualPublished = false;
        try
        {
            using (new AllowedThread()) visual.OnMapEventEnd();
            return true;
        }
        catch
        {
            if (state != null) state.VisualPublished = true;
            throw;
        }
    }

    public void DestroyGraph(MapEvent mapEvent)
    {
        if (mapEvent == null) return;

        if (!states.TryGetValue(mapEvent, out var state)) state = new State(Array.Empty<object>());
        AddOwned(state, MapEventGraph.Enumerate(mapEvent));

        RemoveFromManager(mapEvent);
        RollBackPartyEdges(mapEvent, state.Owned);
        ReleaseClientParties(state);

        if (ModInformation.IsClient)
        {
            foreach (var visual in state.Owned.OfType<GauntletMapEventVisual>().ToArray())
            {
                try
                {
                    EndVisual(visual);
                }
                catch (Exception ex)
                {
                    Logger.Warning(ex, "Failed to end MapEvent visual during graph teardown");
                }
            }
        }

        states.Remove(mapEvent);
        foreach (var instance in state.Owned)
            objectManager.Remove(instance);
    }

    private void PublishVisual(GauntletMapEventVisual visual, CampaignVec2 position)
    {
        var creator = Campaign.Current?.VisualCreator?.MapEventVisualCreator as GauntletMapEventVisualCreator;
        if (creator != null && !creator.GetCurrentEvents().Contains(visual))
        {
            creator.Handlers?.ForEach(handler => handler.OnNewEventStarted(visual));
            creator._listOfEvents.Add(visual);
        }

        using (new AllowedThread())
        {
            visual.Initialize(position, visual.MapEvent.IsVisible);
        }
    }

    private static bool IsComplete(MapEvent mapEvent, State state) =>
        HasCompleteSides(mapEvent) &&
        HasCompleteComponent(mapEvent) &&
        mapEvent._sides.All(side => IsCompleteSide(mapEvent, side)) &&
        MapEventGraph.GetTracker(mapEvent) != null &&
        HasCompleteVisual(mapEvent, state);

    private static bool HasCompleteSides(MapEvent mapEvent) =>
        mapEvent._sides != null && mapEvent._sides.Length >= 2;

    private static bool HasCompleteComponent(MapEvent mapEvent) =>
        mapEvent.Component == null || ReferenceEquals(mapEvent.Component.MapEvent, mapEvent);

    private static bool IsCompleteSide(MapEvent mapEvent, MapEventSide side) =>
        side != null &&
        ReferenceEquals(side.MapEvent, mapEvent) &&
        side.Parties != null &&
        side.Parties.Count > 0 &&
        side.Parties.All(IsCompleteParty);

    private static bool IsCompleteParty(MapEventParty party) =>
        party?.Party != null &&
        party._roster != null &&
        party._woundedInBattle != null &&
        party._diedInBattle != null &&
        party._routedInBattle != null;

    private static bool HasCompleteVisual(MapEvent mapEvent, State state) =>
        mapEvent.MapEventVisual is not GauntletMapEventVisual visual ||
        (ReferenceEquals(visual.MapEvent, mapEvent) && ReferenceEquals(state.Visual, visual));

    private static void RemoveFromManager(MapEvent mapEvent)
    {
        var manager = Campaign.Current?.MapEventManager;
        if (manager == null) return;
        manager._mapEvents.Remove(mapEvent);
    }

    private static void RollBackPartyEdges(MapEvent mapEvent, IEnumerable<object> graph)
    {
        using (new AllowedThread())
        {
            foreach (var mapEventParty in graph.OfType<MapEventParty>())
            {
                var party = mapEventParty.Party;
                if (party?._mapEventSide != null && ReferenceEquals(party._mapEventSide.MapEvent, mapEvent))
                {
                    party._mapEventSide = null;
                    if (party.MobileParty != null) party.MobileParty.EventPositionAdder = Vec2.Zero;
                    party.SetVisualAsDirty();
                }
            }
        }
    }

    private static void AddOwned(State state, IEnumerable<object> objects)
    {
        foreach (var instance in objects)
        {
            if (instance != null)
                state.Owned.Add(instance);
        }
    }

    private static void TrackClientPartyLock(State state, PartyBase party)
    {
        if (party == null || !state.PendingParties.Add(party)) return;
        PendingMapEventPartyLock.Lock(party, state);
    }

    private static void ReleaseClientParty(State state, PartyBase party)
    {
        if (party == null || !state.PendingParties.Remove(party)) return;
        PendingMapEventPartyLock.Release(party, state);
    }

    private static void ReleaseClientParties(State state)
    {
        foreach (var party in state.PendingParties.ToArray())
            ReleaseClientParty(state, party);
    }

    private sealed class State
    {
        public readonly HashSet<object> Owned = new HashSet<object>();
        public readonly List<(MapEventSide Side, MapEventParty Party)> DeferredEdges =
            new List<(MapEventSide, MapEventParty)>();
        public readonly List<Action> AfterCommit = new List<Action>();
        public readonly HashSet<PartyBase> PendingParties = new HashSet<PartyBase>();
        public readonly HashSet<PartyBase> AnnouncedParties = new HashSet<PartyBase>();
        public bool Committed;
        public bool Terminal;
        public bool VisualPublished;
        public GauntletMapEventVisual Visual;
        public CampaignVec2 VisualPosition;

        public State(IEnumerable<object> objects) => AddOwned(this, objects);
    }
}

internal static class PendingMapEventPartyLock
{
    private sealed class Owners
    {
        public readonly HashSet<object> States = new HashSet<object>();

        public Owners() { }
    }

    private static readonly ConditionalWeakTable<PartyBase, Owners> Locks =
        new ConditionalWeakTable<PartyBase, Owners>();

    public static bool IsLocked(PartyBase party)
    {
        if (party == null || !Locks.TryGetValue(party, out var owners)) return false;
        lock (owners) return owners.States.Count > 0;
    }

    public static void Lock(PartyBase party, object state)
    {
        if (party == null || state == null) return;

        var owners = Locks.GetOrCreateValue(party);
        lock (owners) owners.States.Add(state);
    }

    public static void Release(PartyBase party, object state)
    {
        if (party == null || state == null) return;

        if (!Locks.TryGetValue(party, out var owners)) return;
        lock (owners)
        {
            owners.States.Remove(state);
            if (owners.States.Count == 0)
                Locks.Remove(party);
        }
    }
}

internal static class MapEventGraph
{
    private static readonly AccessTools.FieldRef<MapEvent, TroopUpgradeTracker> TrackerField =
        AccessTools.FieldRefAccess<MapEvent, TroopUpgradeTracker>("<TroopUpgradeTracker>k__BackingField");

    public static TroopUpgradeTracker GetTracker(MapEvent mapEvent) =>
        mapEvent == null ? null : TrackerField(mapEvent);

    public static IEnumerable<object> Enumerate(MapEvent mapEvent)
    {
        if (mapEvent == null) yield break;
        yield return mapEvent;
        yield return mapEvent.Component;
        yield return GetTracker(mapEvent);
        yield return mapEvent.MapEventVisual;

        if (mapEvent._sides == null) yield break;
        foreach (var side in mapEvent._sides)
        {
            yield return side;
            if (side?.Parties == null) continue;
            foreach (var party in side.Parties)
                foreach (var instance in EnumerateParty(party))
                    yield return instance;
        }
    }

    public static IEnumerable<object> EnumerateParty(MapEventParty party)
    {
        yield return party;
        if (party == null) yield break;
        yield return party._woundedInBattle;
        yield return party._diedInBattle;
        yield return party._routedInBattle;
    }
}
