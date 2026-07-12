using Common;
using Common.Logging;
using Common.Messaging;
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
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Library;

namespace GameInterface.Services.MapEvents.Initialization;

public interface IMapEventInitializationBarrier : IGameAbstraction
{
    bool IsPending(MapEvent mapEvent);
    bool IsPartyPending(PartyBase party);
    void Register(MapEvent mapEvent, bool committed = false);
    void SetServerPartyPending(MapEvent mapEvent, PartyBase party, bool pending);
    void CommitServer(MapEvent mapEvent);
    void AbortServer(MapEvent mapEvent);
    void AttachClient(MapEventSide side, MapEventParty party, Action afterCommit = null);
    void TrackParty(MapEvent mapEvent, MapEventParty party);
    void DeferVisual(GauntletMapEventVisual visual, CampaignVec2 position);
    void DestroyGraph(MapEvent mapEvent);
}

internal sealed class MapEventInitializationBarrier : IMapEventInitializationBarrier, IDisposable
{
    private static readonly ILogger Logger = LogManager.GetLogger<MapEventInitializationBarrier>();
    private static readonly AccessTools.FieldRef<MapEvent, TroopUpgradeTracker> TrackerField =
        AccessTools.FieldRefAccess<MapEvent, TroopUpgradeTracker>("<TroopUpgradeTracker>k__BackingField");

    private readonly IMessageBroker messageBroker;
    private readonly INetwork network;
    private readonly IObjectManager objectManager;
    private readonly Dictionary<MapEvent, State> states = new Dictionary<MapEvent, State>();
    private bool disposed;

    public MapEventInitializationBarrier(IMessageBroker messageBroker, INetwork network, IObjectManager objectManager)
    {
        this.messageBroker = messageBroker;
        this.network = network;
        this.objectManager = objectManager;
        messageBroker.Subscribe<NetworkMapEventPartyPending>(HandlePendingParty);
        messageBroker.Subscribe<NetworkMapEventInitialized>(HandleCommit);
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        messageBroker.Unsubscribe<NetworkMapEventPartyPending>(HandlePendingParty);
        messageBroker.Unsubscribe<NetworkMapEventInitialized>(HandleCommit);
        states.Clear();
    }

    public bool IsPending(MapEvent mapEvent) =>
        mapEvent != null && states.TryGetValue(mapEvent, out var state) && !state.Committed;

    public bool IsPartyPending(PartyBase party) =>
        party != null && states.Values.Any(state => state.Parties.Contains(party) || state.Announced.Contains(party));

    public void Register(MapEvent mapEvent, bool committed = false)
    {
        if (disposed || mapEvent == null) return;
        if (!states.TryGetValue(mapEvent, out var state)) states.Add(mapEvent, state = new State(mapEvent));
        if (!committed) return;
        Capture(state, mapEvent);
        state.Committed = true;
        state.Parties.Clear();
    }

    public void SetServerPartyPending(MapEvent mapEvent, PartyBase party, bool pending)
    {
        if (mapEvent == null || party == null || !states.TryGetValue(mapEvent, out var state)) return;
        if (!state.Committed && !(pending ? state.Announced.Add(party) : state.Announced.Remove(party))) return;
        if (objectManager.TryGetIdWithLogging(mapEvent, out var mapEventId) &&
            objectManager.TryGetIdWithLogging(party, out var partyId))
        {
            network.SendAll(new NetworkMapEventPartyPending(mapEventId, partyId, !pending));
            return;
        }
        if (!state.Committed)
        {
            if (pending) state.Announced.Remove(party); else state.Announced.Add(party);
        }
    }

    public void CommitServer(MapEvent mapEvent)
    {
        if (mapEvent == null || !states.TryGetValue(mapEvent, out var state) || state.Committed) return;
        var tracker = GetTracker(mapEvent);
        if (tracker == null)
        {
            mapEvent.TroopUpgradeTracker = tracker = new TroopUpgradeTracker();
            foreach (var side in mapEvent._sides ?? Array.Empty<MapEventSide>())
                foreach (var party in side?.Parties ?? Enumerable.Empty<MapEventParty>())
                    tracker.AddParty(party);
        }
        Capture(state, mapEvent);
        if (!TryGetId(mapEvent, out var mapEventId) ||
            !TryGetId(tracker, out var trackerId) || !TryGetId(mapEvent.Component, out var componentId) ||
            !TryGetId(mapEvent.MapEventVisual as GauntletMapEventVisual, out var visualId))
        {
            AbortServer(mapEvent);
            return;
        }

        network.SendAll(new NetworkMapEventInitialized(mapEventId, false, trackerId, componentId, visualId));
        state.Committed = true;
        state.Announced.Clear();
    }

    public void AbortServer(MapEvent mapEvent)
    {
        if (mapEvent == null || !states.TryGetValue(mapEvent, out var state) || state.Committed) return;
        if (TryGetId(mapEvent, out var id)) network.SendAll(new NetworkMapEventInitialized(id, true));
        DestroyGraph(mapEvent);
    }

    private bool TryGetId(object instance, out string id)
    {
        id = null;
        return instance == null || objectManager.TryGetIdWithLogging(instance, out id);
    }

    private void HandlePendingParty(MessagePayload<NetworkMapEventPartyPending> payload)
    {
        var message = payload.What;
        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<MapEvent>(message.MapEventId, out var mapEvent) ||
                !objectManager.TryGetObjectWithLogging<PartyBase>(message.PartyId, out var party)) return;
            Register(mapEvent);
            if (message.IsCancellation) states[mapEvent].Parties.Remove(party);
            else states[mapEvent].Parties.Add(party);
        }, context: nameof(NetworkMapEventPartyPending));
    }

    private void HandleCommit(MessagePayload<NetworkMapEventInitialized> payload)
    {
        var message = payload.What;
        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<MapEvent>(message.MapEventId, out var mapEvent)) return;
            if (message.IsTerminal || string.IsNullOrEmpty(message.TroopUpgradeTrackerId) ||
                !objectManager.TryGetObjectWithLogging<TroopUpgradeTracker>(message.TroopUpgradeTrackerId, out var tracker) ||
                !Matches(message.ComponentId, mapEvent.Component) ||
                !Matches(message.VisualId, mapEvent.MapEventVisual as GauntletMapEventVisual))
            {
                FinishClient(mapEvent, abort: true);
                return;
            }

            using (new AllowedThread()) mapEvent.TroopUpgradeTracker = tracker;
            FinishClient(mapEvent, abort: false);
        }, context: nameof(NetworkMapEventInitialized));
    }

    private bool Matches<T>(string id, T actual) where T : class =>
        id == null ? actual == null :
        objectManager.TryGetObjectWithLogging<T>(id, out var expected) && ReferenceEquals(expected, actual);

    private void FinishClient(MapEvent mapEvent, bool abort)
    {
        Register(mapEvent);
        if (!states.TryGetValue(mapEvent, out var state)) return;
        Capture(state, mapEvent);
        if (abort)
        {
            DestroyGraph(mapEvent);
            return;
        }
        if (state.Committed) return;
        if (!IsComplete(mapEvent, state))
        {
            Logger.Error("MapEvent {MapEventId} reached its commit with an incomplete graph", mapEvent.StringId);
            DestroyGraph(mapEvent);
            return;
        }

        try
        {
            using (new AllowedThread())
            {
                var tracker = GetTracker(mapEvent);
                foreach (var side in mapEvent._sides)
                    foreach (var party in side.Parties)
                    {
                        party.Party._mapEventSide = side;
                        tracker.AddParty(party);
                    }

                var manager = Campaign.Current?.MapEventManager;
                if (manager != null && !manager.MapEvents.Contains(mapEvent)) manager.OnMapEventCreated(mapEvent);
            }

            if (state.Visual != null) PublishVisual(state.Visual, state.Position);
            state.Committed = true;
            state.Parties.Clear();
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Failed to commit MapEvent {MapEventId}; rolling it back", mapEvent.StringId);
            DestroyGraph(mapEvent);
            return;
        }

        var callback = state.Callback;
        state.Callback = null;
        if (callback != null) GameThread.RunSafe(callback, context: "MapEvent commit callback");
    }

    public void AttachClient(MapEventSide side, MapEventParty party, Action afterCommit = null)
    {
        if (disposed || side?.MapEvent == null || party == null) return;
        states.TryGetValue(side.MapEvent, out var state);
        if (state != null) Capture(state, party);
        using (new AllowedThread())
        {
            if (!side._battleParties.Contains(party)) side._battleParties.Add(party);
            if (state?.Committed != false && party.Party != null) party.Party._mapEventSide = side;
        }

        if (state == null || state.Committed)
        {
            state?.Parties.Remove(party.Party);
            afterCommit?.Invoke();
            return;
        }

        state.Parties.Add(party.Party);
        state.Callback += afterCommit;
    }

    public void TrackParty(MapEvent mapEvent, MapEventParty party)
    {
        if (mapEvent != null && party != null && states.TryGetValue(mapEvent, out var state)) Capture(state, party);
    }

    public void DeferVisual(GauntletMapEventVisual visual, CampaignVec2 position)
    {
        if (disposed || visual?.MapEvent == null) return;
        if (states.TryGetValue(visual.MapEvent, out var state))
        {
            state.Owned.Add(visual);
            if (!state.Committed)
            {
                state.Visual = visual;
                state.Position = position;
                return;
            }
        }
        PublishVisual(visual, position);
    }

    private void EndVisual(GauntletMapEventVisual visual)
    {
        var creator = Campaign.Current?.VisualCreator?.MapEventVisualCreator as GauntletMapEventVisualCreator;
        if (visual == null || creator?.GetCurrentEvents().Contains(visual) != true) return;
        using (new AllowedThread()) visual.OnMapEventEnd();
    }

    public void DestroyGraph(MapEvent mapEvent)
    {
        if (mapEvent == null) return;
        if (!states.TryGetValue(mapEvent, out var state)) state = new State(mapEvent);
        Capture(state, mapEvent);
        Campaign.Current?.MapEventManager?._mapEvents.Remove(mapEvent);
        foreach (var mapEventParty in state.Owned.OfType<MapEventParty>())
        {
            var party = mapEventParty.Party;
            if (party == null || (party._mapEventSide != null && party._mapEventSide.MapEvent != mapEvent)) continue;
            if (party._mapEventSide?.MapEvent == mapEvent) party._mapEventSide = null;
            if (party.MobileParty != null) party.MobileParty.EventPositionAdder = Vec2.Zero;
            party.SetVisualAsDirty();
        }

        foreach (var visual in state.Owned.OfType<GauntletMapEventVisual>().ToArray()) EndVisual(visual);
        states.Remove(mapEvent);
        foreach (var instance in state.Owned) objectManager.Remove(instance);
    }

    private static void PublishVisual(GauntletMapEventVisual visual, CampaignVec2 position)
    {
        var creator = Campaign.Current?.VisualCreator?.MapEventVisualCreator as GauntletMapEventVisualCreator;
        if (creator != null && !creator.GetCurrentEvents().Contains(visual))
        {
            creator.Handlers?.ForEach(handler => handler.OnNewEventStarted(visual));
            creator._listOfEvents.Add(visual);
        }
        using (new AllowedThread()) visual.Initialize(position, visual.MapEvent.IsVisible);
    }

    private static bool IsComplete(MapEvent mapEvent, State state) =>
        mapEvent._sides?.Length >= 2 &&
        mapEvent._sides.All(side => side != null && ReferenceEquals(side.MapEvent, mapEvent) &&
            side.Parties?.Count > 0 && side.Parties.All(party => party?.Party != null &&
                party._roster != null && party._woundedInBattle != null &&
                party._diedInBattle != null && party._routedInBattle != null)) &&
        (mapEvent.Component == null || ReferenceEquals(mapEvent.Component.MapEvent, mapEvent)) &&
        GetTracker(mapEvent) != null &&
        (mapEvent.MapEventVisual is not GauntletMapEventVisual visual || ReferenceEquals(state.Visual, visual));

    internal static TroopUpgradeTracker GetTracker(MapEvent mapEvent) => mapEvent == null ? null : TrackerField(mapEvent);

    private static void Capture(State state, MapEvent mapEvent)
    {
        if (mapEvent == null) return;
        state.Add(mapEvent);
        state.Add(mapEvent.Component);
        state.Add(GetTracker(mapEvent));
        state.Add(mapEvent.MapEventVisual);
        foreach (var side in mapEvent._sides ?? Array.Empty<MapEventSide>())
        {
            state.Add(side);
            if (side?.Parties == null) continue;
            foreach (var party in side.Parties) Capture(state, party);
        }
    }

    private static void Capture(State state, MapEventParty party)
    {
        state.Add(party);
        if (party == null) return;
        state.Add(party._woundedInBattle);
        state.Add(party._diedInBattle);
        state.Add(party._routedInBattle);
    }

    private sealed class State
    {
        public readonly HashSet<object> Owned = new HashSet<object>();
        public readonly HashSet<PartyBase> Parties = new HashSet<PartyBase>();
        public readonly HashSet<PartyBase> Announced = new HashSet<PartyBase>();
        public bool Committed;
        public GauntletMapEventVisual Visual;
        public CampaignVec2 Position;
        public Action Callback;

        public State(object instance) => Add(instance);
        public void Add(object instance)
        {
            if (instance != null) Owned.Add(instance);
        }
    }
}
