using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using Common.Util;
using GameInterface.Registry.Auto;
using GameInterface.Services.GuantletMapEventVisuals;
using GameInterface.Services.MapEventParties;
using GameInterface.Services.MapEvents.Initialization;
using GameInterface.Services.MapEvents.Messages.Start;
using GameInterface.Services.ObjectManager;
using HarmonyLib;
using SandBox.GauntletUI.Map;
using Serilog;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace GameInterface.Services.MapEvents.Handlers;

/// <summary>
/// Publishes and applies a MapEvent's initial object graph as one transaction. Ordinary lifetime and
/// AutoSync traffic resumes after this handler commits the graph, so later reinforcement deltas keep
/// using the existing replication paths.
/// </summary>
internal sealed class MapEventInitializationHandler : IHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger<MapEventInitializationHandler>();

    private readonly IMessageBroker messageBroker;
    private readonly INetwork network;
    private readonly IMapEventInitializationSnapshotFactory snapshotFactory;
    private readonly IMapEventInitializationTracker initializationTracker;
    private readonly IMapEventInitializationHydrator hydrator;

    public MapEventInitializationHandler(
        IMessageBroker messageBroker,
        INetwork network,
        IMapEventInitializationSnapshotFactory snapshotFactory,
        IMapEventInitializationTracker initializationTracker,
        IMapEventInitializationHydrator hydrator)
    {
        this.messageBroker = messageBroker;
        this.network = network;
        this.snapshotFactory = snapshotFactory;
        this.initializationTracker = initializationTracker;
        this.hydrator = hydrator;

        messageBroker.Subscribe<NetworkInitializeMapEvent>(Handle_NetworkInitializeMapEvent);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<NetworkInitializeMapEvent>(Handle_NetworkInitializeMapEvent);
    }

    /// <summary>
    /// Server commit point. Request-driven events publish after their vanilla factory returns; ordinary
    /// events publish in the prefix of their first manager tick.
    /// </summary>
    internal void Publish(MapEvent mapEvent, bool terminalInitialization = false)
    {
        if (mapEvent == null) throw new ArgumentNullException(nameof(mapEvent));
        if (ModInformation.IsClient) return;
        if (!initializationTracker.IsBuilding(mapEvent)) return;
        if (initializationTracker.IsPublished(mapEvent)) return;

        try
        {
            // Fully materialize and validate everything which can throw before any peer sees the command.
            var graph = new List<object>(MapEventGraph.Enumerate(mapEvent));
            if (!snapshotFactory.TryCreate(mapEvent, terminalInitialization, out var message))
            {
                throw new InvalidOperationException(
                    "Cannot publish a MapEvent whose initialization graph contains an unregistered or invalid reference");
            }

            network.SendAll(message);

            if (terminalInitialization && initializationTracker.IsInitializing(mapEvent))
                initializationTracker.MarkPublished(mapEvent, graph);
            else
                initializationTracker.CompleteBuild(mapEvent, graph);
        }
        catch
        {
            initializationTracker.AbortBuild(mapEvent);
            throw;
        }
    }

    private void Handle_NetworkInitializeMapEvent(MessagePayload<NetworkInitializeMapEvent> payload)
    {
        if (ModInformation.IsServer) return;

        var message = payload.What;
        GameThread.RunSafe(() =>
        {
            try
            {
                using (new AllowedThread())
                {
                    if (!hydrator.TryApply(message))
                    {
                        Logger.Error(
                            "Rejected incomplete or inconsistent aggregate MapEvent initialization {MapEventId}",
                            message?.MapEventId);
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error(
                    ex,
                    "Failed to apply aggregate MapEvent initialization {MapEventId}",
                    message?.MapEventId);
            }
        }, context: nameof(NetworkInitializeMapEvent));
    }

}

internal interface IMapEventInitializationHydrator : IGameAbstraction
{
    bool TryApply(NetworkInitializeMapEvent message);
}

/// <summary>
/// Client-side half of the transaction. External references are resolved before any graph object is
/// created; internal objects are then wired off-registry and registered in one atomic batch.
/// </summary>
internal sealed class MapEventInitializationHydrator : IMapEventInitializationHydrator
{
    private static readonly ILogger Logger = LogManager.GetLogger<MapEventInitializationHydrator>();

    private static readonly System.Reflection.FieldInfo MapEventSideMapEventField =
        AccessTools.Field(typeof(MapEventSide), nameof(MapEventSide._mapEvent));

    private static readonly System.Reflection.FieldInfo HideoutIsSendTroopsField =
        AccessTools.Field(typeof(HideoutEventComponent), nameof(HideoutEventComponent.IsSendTroops));

    private static readonly System.Reflection.FieldInfo GauntletVisualMapEventField =
        AccessTools.Field(typeof(GauntletMapEventVisual), "<MapEvent>k__BackingField");

    private static readonly System.Reflection.FieldInfo GauntletVisualOnDeactivateField =
        AccessTools.Field(typeof(GauntletMapEventVisual), "_onDeactivate");

    private static readonly System.Reflection.FieldInfo GauntletVisualOnInitializedField =
        AccessTools.Field(typeof(GauntletMapEventVisual), "_onInitialized");

    private static readonly System.Reflection.FieldInfo GauntletVisualOnVisibilityChangedField =
        AccessTools.Field(typeof(GauntletMapEventVisual), "_onVisibilityChanged");

    private static readonly System.Reflection.FieldInfo GauntletVisualListField =
        AccessTools.Field(typeof(GauntletMapEventVisualCreator), "_listOfEvents");

    private readonly IObjectManager objectManager;
    private readonly IAutoRegistryFactory autoRegistryFactory;
    private readonly IMapEventBattleSizeCorrection battleSizeCorrection;
    private readonly IMapEventInitializationTracker initializationTracker;

    public MapEventInitializationHydrator(
        IObjectManager objectManager,
        IAutoRegistryFactory autoRegistryFactory,
        IMapEventBattleSizeCorrection battleSizeCorrection,
        IMapEventInitializationTracker initializationTracker)
    {
        this.objectManager = objectManager;
        this.autoRegistryFactory = autoRegistryFactory;
        this.battleSizeCorrection = battleSizeCorrection;
        this.initializationTracker = initializationTracker;
    }

    public bool TryApply(NetworkInitializeMapEvent message)
    {
        if (message == null || string.IsNullOrEmpty(message.MapEventId))
            return Invalid("Aggregate MapEvent initialization had no root id");

        if (Campaign.Current?.MapEventManager == null)
            return Invalid("Cannot initialize MapEvent {MapEventId} without an active campaign", message.MapEventId);

        if (objectManager.Contains(message.MapEventId))
            return Invalid("MapEvent initialization id {MapEventId} is already registered", message.MapEventId);

        if (!TryResolveGraph(message, out var graph))
            return false;

        if (!TryCreateGraph(message, graph))
            return false;

        WireGraph(message, graph);
        // A terminal aggregate exists only so the immediately-following destroy stream can resolve
        // every internal id. Publishing its external PartyBase edges would briefly resurrect an event
        // which was already finalized on the server, and a later cleanup could detach a newer battle.
        bool externalEdgesApplied = !message.IsTerminalInitialization;
        if (externalEdgesApplied)
            ApplyExternalEdges(graph);

        try
        {
            if (!TryCreateVisual(message, graph))
            {
                if (externalEdgesApplied)
                    RollBackExternalEdges(graph);
                return false;
            }
        }
        catch (Exception ex)
        {
            DiscardLocalVisual(graph.Visual);
            if (externalEdgesApplied)
                RollBackExternalEdges(graph);
            Logger.Error(
                ex,
                "Could not publish local visual for MapEvent {MapEventId}: {ExceptionType}: {ExceptionMessage}; inner: {InnerException}",
                message.MapEventId,
                ex.GetType().FullName,
                ex.Message,
                ex.InnerException?.ToString());
            return false;
        }

        graph.MapEvent.MapEventVisual = graph.Visual;

        var registrations = BuildRegistrations(message, graph);
        if (!objectManager.AddExistingBatch(registrations))
        {
            DiscardLocalVisual(graph.Visual);
            if (externalEdgesApplied)
                RollBackExternalEdges(graph);
            return Invalid("Could not atomically register MapEvent graph {MapEventId}", message.MapEventId);
        }

        initializationTracker.RegisterCommittedGraph(
            graph.MapEvent,
            registrations.ConvertAll(pair => pair.Value));

        InitializeVisual(message, graph);

        // Manager membership is the public commit marker. No game tick can observe this event before
        // every id, edge, roster, tracker entry, position, and visual has been established.
        if (!message.IsTerminalInitialization)
            Campaign.Current.MapEventManager.OnMapEventCreated(graph.MapEvent);

        return true;
    }

    private bool TryResolveGraph(NetworkInitializeMapEvent message, out ResolvedGraph graph)
    {
        graph = null;

        if (message.DefenderSide == null || message.AttackerSide == null)
            return Invalid("MapEvent {MapEventId} did not contain both sides", message.MapEventId);

        if (message.StrengthOfSide == null || message.StrengthOfSide.Length != 2 ||
            message.WonRounds == null || string.IsNullOrEmpty(message.TroopUpgradeTrackerId))
        {
            return Invalid("MapEvent {MapEventId} had invalid root collections or tracker id", message.MapEventId);
        }

        Settlement settlement = null;
        if (message.MapEventSettlementId != null &&
            !objectManager.TryGetObjectWithLogging(message.MapEventSettlementId, out settlement))
        {
            return false;
        }

        if (!TryResolveComponent(message, out var component))
            return false;

        var usedParties = new HashSet<PartyBase>(ReferenceComparer<PartyBase>.Instance);
        if (!TryResolveSide(
                message,
                message.DefenderSide,
                BattleSideEnum.Defender,
                usedParties,
                out var defenderSide) ||
            !TryResolveSide(
                message,
                message.AttackerSide,
                BattleSideEnum.Attacker,
                usedParties,
                out var attackerSide))
        {
            return false;
        }

        graph = new ResolvedGraph(settlement, component, defenderSide, attackerSide);
        return true;
    }

    private bool TryResolveComponent(
        NetworkInitializeMapEvent message,
        out ResolvedComponent component)
    {
        component = null;
        var data = message.Component;
        if (data == null) return true;

        if (string.IsNullOrEmpty(data.ComponentId) || data.MapEventId != message.MapEventId ||
            !MapEventComponentKindMapper.TryGetComponentType(data.Kind, out var componentType) ||
            componentType == null)
        {
            return Invalid("MapEvent {MapEventId} had invalid component metadata", message.MapEventId);
        }

        var rewards = new Dictionary<ItemObject, float>();
        var rewardData = data.RaidProductionRewards ?? Array.Empty<RaidProductionRewardInitializationData>();
        foreach (var reward in rewardData)
        {
            if (reward == null || string.IsNullOrEmpty(reward.ItemObjectId) ||
                !objectManager.TryGetObjectWithLogging(reward.ItemObjectId, out ItemObject item))
            {
                return false;
            }

            if (rewards.ContainsKey(item))
                return Invalid("MapEvent {MapEventId} contained a duplicate raid reward item", message.MapEventId);

            rewards.Add(item, reward.Amount);
        }

        component = new ResolvedComponent(data, componentType, rewards);
        return true;
    }

    private bool TryResolveSide(
        NetworkInitializeMapEvent message,
        MapEventSideInitializationData data,
        BattleSideEnum expectedSide,
        HashSet<PartyBase> usedParties,
        out ResolvedSide side)
    {
        side = null;

        if (string.IsNullOrEmpty(data.MapEventSideId) || data.MapEventId != message.MapEventId ||
            data.MissionSide != expectedSide || data.Parties == null ||
            (!message.IsTerminalInitialization && data.Parties.Length == 0))
        {
            return Invalid(
                "MapEvent {MapEventId} had invalid {BattleSide} side metadata",
                message.MapEventId,
                expectedSide);
        }

        if (!objectManager.TryGetObjectWithLogging(data.LeaderPartyId, out PartyBase leaderParty) ||
            !objectManager.TryGetObjectWithLogging(data.MapFactionId, out IFaction faction))
        {
            return false;
        }

        var parties = new List<ResolvedParty>(data.Parties.Length);
        foreach (var partyData in data.Parties)
        {
            if (!TryResolveParty(message, data, partyData, usedParties, out var party))
                return false;

            parties.Add(party);
        }

        if (!message.IsTerminalInitialization &&
            !parties.Exists(party => ReferenceEquals(party.PartyBase, leaderParty)))
        {
            return Invalid(
                "MapEvent {MapEventId} side {BattleSide} did not contain its leader",
                message.MapEventId,
                expectedSide);
        }

        side = new ResolvedSide(data, leaderParty, faction, parties);
        return true;
    }

    private bool TryResolveParty(
        NetworkInitializeMapEvent message,
        MapEventSideInitializationData sideData,
        MapEventPartyInitializationData data,
        HashSet<PartyBase> usedParties,
        out ResolvedParty party)
    {
        party = null;
        if (data == null || string.IsNullOrEmpty(data.MapEventPartyId) ||
            data.MapEventSideId != sideData.MapEventSideId ||
            string.IsNullOrEmpty(data.WoundedInBattleRosterId) ||
            string.IsNullOrEmpty(data.DiedInBattleRosterId) ||
            string.IsNullOrEmpty(data.RoutedInBattleRosterId))
        {
            return Invalid("MapEvent {MapEventId} contained invalid party metadata", message.MapEventId);
        }

        if (!objectManager.TryGetObjectWithLogging(data.PartyBaseId, out PartyBase partyBase))
            return false;

        if (!usedParties.Add(partyBase))
            return Invalid("MapEvent {MapEventId} contained PartyBase {PartyId} more than once", message.MapEventId, data.PartyBaseId);

        if (partyBase.MapEventSide != null)
            return Invalid("PartyBase {PartyId} already belongs to a MapEvent", data.PartyBaseId);

        var mobileParty = partyBase.MobileParty;
        if (data.HasMobileParty != (mobileParty != null))
            return Invalid("PartyBase {PartyId} mobile-party shape differs from the server", data.PartyBaseId);

        if (!TryDeserializeFlattenedRoster(message.MapEventId, data.FlattenedTroops, out var roster))
            return false;

        party = new ResolvedParty(data, partyBase, roster);
        return true;
    }

    private bool TryDeserializeFlattenedRoster(
        string mapEventId,
        FlattenedTroop[] troops,
        out FlattenedTroopRoster roster)
    {
        roster = null;
        if (troops == null)
            return Invalid("MapEvent {MapEventId} contained a null flattened roster", mapEventId);

        var uniqueSeeds = new HashSet<int>();
        roster = new FlattenedTroopRoster(troops.Length);

        foreach (var troop in troops)
        {
            if (string.IsNullOrEmpty(troop.ObjectId) || !uniqueSeeds.Add(troop.UniqueSeed))
                return Invalid("MapEvent {MapEventId} contained an invalid flattened troop", mapEventId);

            CharacterObject character;
            if (troop.IsHero)
            {
                if (!objectManager.TryGetObjectWithLogging(troop.ObjectId, out Hero hero) ||
                    (character = hero.CharacterObject) == null)
                {
                    return false;
                }
            }
            else if (!objectManager.TryGetObjectWithLogging(troop.ObjectId, out character))
            {
                return false;
            }

            var descriptor = new UniqueTroopDescriptor(troop.UniqueSeed);
            roster[descriptor] = new FlattenedTroopRosterElement(
                character,
                troop.State,
                troop.Xp,
                descriptor,
                troop.XpGained);
        }

        return true;
    }

    private bool TryCreateGraph(NetworkInitializeMapEvent message, ResolvedGraph graph)
    {
        if (!TryCreate(message.MapEventId, out MapEvent mapEvent) ||
            !TryCreate(message.TroopUpgradeTrackerId, out TroopUpgradeTracker tracker))
        {
            return false;
        }

        graph.MapEvent = mapEvent;
        graph.Tracker = tracker;

        if (graph.Component != null)
        {
            if (!autoRegistryFactory.TryCreateClientObject(
                    graph.Component.ComponentType,
                    graph.Component.Data.ComponentId,
                    out var componentObject) ||
                componentObject is not MapEventComponent component)
            {
                return Invalid("Could not create component {ComponentId}", graph.Component.Data.ComponentId);
            }

            graph.Component.Instance = component;
        }

        if (!TryCreateSide(graph.DefenderSide) || !TryCreateSide(graph.AttackerSide))
            return false;

        return true;
    }

    private bool TryCreateVisual(NetworkInitializeMapEvent message, ResolvedGraph graph)
    {
        if (message.IsTerminalInitialization)
        {
            if (message.GauntletMapEventVisualId == null)
                return true;

            var terminalVisual = ObjectHelper.SkipConstructor<GauntletMapEventVisual>();
            GauntletVisualMapEventField.SetValue(terminalVisual, graph.MapEvent);
            graph.Visual = terminalVisual;
            return true;
        }

        var visualCreator = Campaign.Current.VisualCreator?.MapEventVisualCreator;
        if (visualCreator is GauntletMapEventVisualCreator gauntletCreator)
        {
            var gauntletVisual = ObjectHelper.SkipConstructor<GauntletMapEventVisual>();
            GauntletVisualMapEventField.SetValue(gauntletVisual, graph.MapEvent);
            GauntletVisualOnInitializedField.SetValue(
                gauntletVisual,
                new Action<GauntletMapEventVisual>(visual => NotifyVisualInitialized(gauntletCreator, visual)));
            GauntletVisualOnVisibilityChangedField.SetValue(
                gauntletVisual,
                new Action<GauntletMapEventVisual>(visual => NotifyVisualVisibilityChanged(gauntletCreator, visual)));
            GauntletVisualOnDeactivateField.SetValue(
                gauntletVisual,
                new Action<GauntletMapEventVisual>(visual => NotifyVisualEnded(gauntletCreator, visual)));
            graph.Visual = gauntletVisual;
            graph.MapEvent.MapEventVisual = gauntletVisual;

            // Reproduce the creator's publication only after the root, both sides, every party and every
            // external PartyBase edge are fully wired. Vanilla CreateMapEventVisual publishes from inside
            // its constructor path, which would expose a partial root to UI observers.
            if (gauntletCreator.Handlers != null)
            {
                foreach (var handler in gauntletCreator.Handlers)
                    handler.OnNewEventStarted(gauntletVisual);
            }

            if (GauntletVisualListField.GetValue(gauntletCreator) is List<GauntletMapEventVisual> visuals)
                visuals.Add(gauntletVisual);

            return true;
        }

        // Non-Gauntlet creators are not registry-managed. The core graph is already fully wired at this
        // point, so their synchronous callbacks still observe a complete MapEvent.
        graph.Visual = Campaign.Current.VisualCreator?.CreateMapEventVisual(graph.MapEvent);
        if (message.GauntletMapEventVisualId != null && graph.Visual is not GauntletMapEventVisual)
        {
            DiscardLocalVisual(graph.Visual);
            return Invalid(
                "MapEvent {MapEventId} expected a Gauntlet visual but the client could not create one",
                message.MapEventId);
        }

        return true;
    }

    private static void NotifyVisualInitialized(
        GauntletMapEventVisualCreator creator,
        GauntletMapEventVisual visual)
    {
        creator.Handlers?.ForEach(handler => handler.OnInitialized(visual));
    }

    private static void NotifyVisualVisibilityChanged(
        GauntletMapEventVisualCreator creator,
        GauntletMapEventVisual visual)
    {
        creator.Handlers?.ForEach(handler => handler.OnEventVisibilityChanged(visual));
    }

    private static void NotifyVisualEnded(
        GauntletMapEventVisualCreator creator,
        GauntletMapEventVisual visual)
    {
        if (GauntletVisualListField.GetValue(creator) is List<GauntletMapEventVisual> visuals)
            visuals.Remove(visual);

        creator.Handlers?.ForEach(handler => handler.OnEventEnded(visual));
    }

    private static void DiscardLocalVisual(IMapEventVisual visual)
    {
        try
        {
            visual?.OnMapEventEnd();
        }
        catch (Exception ex)
        {
            Logger.Warning(ex, "Failed to discard an uncommitted local MapEvent visual");
        }
    }

    private bool TryCreateSide(ResolvedSide side)
    {
        if (!TryCreate(side.Data.MapEventSideId, out MapEventSide instance))
            return false;

        side.Instance = instance;
        foreach (var party in side.Parties)
        {
            if (!TryCreate(party.Data.MapEventPartyId, out MapEventParty mapEventParty) ||
                !TryCreate(party.Data.WoundedInBattleRosterId, out TroopRoster wounded) ||
                !TryCreate(party.Data.DiedInBattleRosterId, out TroopRoster died) ||
                !TryCreate(party.Data.RoutedInBattleRosterId, out TroopRoster routed))
            {
                return false;
            }

            party.Instance = mapEventParty;
            party.Wounded = wounded;
            party.Died = died;
            party.Routed = routed;
        }

        return true;
    }

    private bool TryCreate<T>(string fullId, out T instance)
        where T : class
    {
        instance = null;
        if (!autoRegistryFactory.TryCreateClientObject(typeof(T), fullId, out var created) ||
            created is not T typed)
        {
            return Invalid("Could not create managed {ObjectType} with id {ObjectId}", typeof(T), fullId);
        }

        instance = typed;
        return true;
    }

    private static void WireGraph(NetworkInitializeMapEvent message, ResolvedGraph graph)
    {
        var mapEvent = graph.MapEvent;
        mapEvent._nextSimulationTime = message.NextSimulationTime;
        mapEvent._mapEventStartTime = message.MapEventStartTime;
        mapEvent._mapEventType = message.EventType;
        mapEvent._eventTerrainType = message.EventTerrainType;
        mapEvent._state = message.State;
        mapEvent._battleState = message.BattleState;
        mapEvent.RetreatingSide = message.RetreatingSide;
        mapEvent.PursuitRoundNumber = message.PursuitRoundNumber;
        mapEvent.Position = message.Position;
        mapEvent.MapEventSettlement = graph.Settlement;
        mapEvent.DiplomaticallyFinished = message.DiplomaticallyFinished;
        mapEvent.IsInvulnerable = message.IsInvulnerable;
        mapEvent.IsPlayerSimulation = message.IsPlayerSimulation;
        mapEvent._mapEventResultsApplied = message.MapEventResultsApplied;
        mapEvent._mapEventResultsCalculated = message.MapEventResultsCalculated;
        mapEvent.FirstUpdateIsDone = message.FirstUpdateIsDone;
        mapEvent._wasEverInLootingPhase = message.WasEverInLootingPhase;
        mapEvent._keepSiegeEvent = message.KeepSiegeEvent;
        mapEvent._isFinishCalled = message.IsFinishCalled;
        mapEvent._playerFigureheadCalculated = message.PlayerFigureheadCalculated;
        mapEvent.StrengthOfSide = (float[])message.StrengthOfSide.Clone();
        mapEvent.WonRounds.Clear();
        foreach (var wonRound in message.WonRounds)
            mapEvent.WonRounds.Add(wonRound);

        mapEvent.Component = graph.Component?.Instance;
        mapEvent.TroopUpgradeTracker = graph.Tracker;
        mapEvent._sides[(int)BattleSideEnum.Defender] = graph.DefenderSide.Instance;
        mapEvent._sides[(int)BattleSideEnum.Attacker] = graph.AttackerSide.Instance;

        WireComponent(mapEvent, graph.Component);
        WireSide(mapEvent, graph.DefenderSide, graph.Tracker);
        WireSide(mapEvent, graph.AttackerSide, graph.Tracker);

        mapEvent._isVisible = IsVisibleToLocalPlayer(mapEvent);
    }

    private static void WireComponent(MapEvent mapEvent, ResolvedComponent component)
    {
        if (component == null) return;

        var instance = component.Instance;
        var data = component.Data;
        instance.MapEvent = mapEvent;
        instance._isFinished = data.IsFinished;

        if (instance is HideoutEventComponent hideout)
        {
            HideoutIsSendTroopsField.SetValue(hideout, data.HideoutIsSendTroops);
        }
        else if (instance is RaidEventComponent raid)
        {
            raid._nextSettlementDamage = data.RaidNextSettlementDamage;
            raid._lootedItemCount = data.RaidLootedItemCount;
            raid._raidProductionRewards = component.RaidProductionRewards;
            raid._isMilitiaResistanceFight = data.RaidIsMilitiaResistanceFight;
            raid.RaidDamage = data.RaidDamage;
        }
        else if (instance is BlockadeBattleMapEvent blockade)
        {
            blockade._isInitializationNotFinished = data.BlockadeIsInitializationNotFinished;
        }
    }

    private static void WireSide(
        MapEvent mapEvent,
        ResolvedSide side,
        TroopUpgradeTracker tracker)
    {
        var instance = side.Instance;
        var data = side.Data;

        MapEventSideMapEventField.SetValue(instance, mapEvent);
        instance.LeaderParty = side.LeaderParty;
        instance._mapFaction = side.Faction;
        instance.MissionSide = data.MissionSide;
        instance.CasualtyStrength = data.CasualtyStrength;
        instance.LeaderSimulationModifier = data.LeaderSimulationModifier;
        instance.StrengthRatio = data.StrengthRatio;
        instance.RenownValue = data.RenownValue;
        instance.InfluenceValue = data.InfluenceValue;
        instance.TroopCasualties = data.TroopCasualties;
        instance.ShipCasualties = data.ShipCasualties;
        instance.IsSurrendered = data.IsSurrendered;

        foreach (var party in side.Parties)
        {
            var mapEventParty = party.Instance;
            var partyData = party.Data;

            mapEventParty.Party = party.PartyBase;
            mapEventParty._woundedInBattle = party.Wounded;
            mapEventParty._diedInBattle = party.Died;
            mapEventParty._routedInBattle = party.Routed;
            mapEventParty._roster = party.FlattenedRoster;
            mapEventParty._contributionToBattle = partyData.ContributionToBattle;
            mapEventParty._healthyManCountAtStart = partyData.HealthyManCountAtStart;
            mapEventParty._participatingTroopCount = partyData.ParticipatingTroopCount;
            mapEventParty.PlunderedGold = partyData.PlunderedGold;
            mapEventParty.GoldLost = partyData.GoldLost;
            mapEventParty.GainedRenownExplained = partyData.GainedRenownExplained;
            mapEventParty.GainedInfluenceExplained = partyData.GainedInfluenceExplained;
            mapEventParty.GainedMoraleExplained = partyData.GainedMoraleExplained;

            instance._battleParties.Add(mapEventParty);
            tracker.AddParty(mapEventParty);
        }
    }

    private static List<KeyValuePair<string, object>> BuildRegistrations(
        NetworkInitializeMapEvent message,
        ResolvedGraph graph)
    {
        var registrations = new List<KeyValuePair<string, object>>
        {
            Pair(message.MapEventId, graph.MapEvent),
            Pair(message.TroopUpgradeTrackerId, graph.Tracker)
        };

        if (graph.Component != null)
            registrations.Add(Pair(graph.Component.Data.ComponentId, graph.Component.Instance));

        if (message.GauntletMapEventVisualId != null)
            registrations.Add(Pair(message.GauntletMapEventVisualId, graph.Visual));

        AddSideRegistrations(registrations, graph.DefenderSide);
        AddSideRegistrations(registrations, graph.AttackerSide);
        return registrations;
    }

    private static void AddSideRegistrations(
        ICollection<KeyValuePair<string, object>> registrations,
        ResolvedSide side)
    {
        registrations.Add(Pair(side.Data.MapEventSideId, side.Instance));
        foreach (var party in side.Parties)
        {
            registrations.Add(Pair(party.Data.MapEventPartyId, party.Instance));
            registrations.Add(Pair(party.Data.WoundedInBattleRosterId, party.Wounded));
            registrations.Add(Pair(party.Data.DiedInBattleRosterId, party.Died));
            registrations.Add(Pair(party.Data.RoutedInBattleRosterId, party.Routed));
        }
    }

    private static KeyValuePair<string, object> Pair(string id, object instance) =>
        new KeyValuePair<string, object>(id, instance);

    private static void ApplyExternalEdges(ResolvedGraph graph)
    {
        ApplyExternalEdges(graph.DefenderSide);
        ApplyExternalEdges(graph.AttackerSide);
    }

    private static void ApplyExternalEdges(ResolvedSide side)
    {
        foreach (var party in side.Parties)
        {
            party.PartyBase._mapEventSide = side.Instance;
            if (party.Data.HasMobileParty)
            {
                party.PartyBase.MobileParty.Position = party.Data.MobilePartyPosition;
                party.PartyBase.MobileParty.EventPositionAdder = new TaleWorlds.Library.Vec2(
                    party.Data.EventPositionAdderX,
                    party.Data.EventPositionAdderY);
            }
        }
    }

    private static void RollBackExternalEdges(ResolvedGraph graph)
    {
        RollBackExternalEdges(graph.DefenderSide);
        RollBackExternalEdges(graph.AttackerSide);
    }

    private static void RollBackExternalEdges(ResolvedSide side)
    {
        foreach (var party in side.Parties)
        {
            if (ReferenceEquals(party.PartyBase._mapEventSide, side.Instance))
                party.PartyBase._mapEventSide = null;

            if (party.PartyBase.MobileParty == null) continue;

            party.PartyBase.MobileParty.Position = party.OriginalMobilePartyPosition;
            party.PartyBase.MobileParty.EventPositionAdder = party.OriginalEventPositionAdder;
        }
    }

    private void InitializeVisual(NetworkInitializeMapEvent message, ResolvedGraph graph)
    {
        if (graph.Visual == null) return;

        try
        {
            graph.Visual.Initialize(message.Position, graph.MapEvent._isVisible);

            if (graph.Visual is GauntletMapEventVisual gauntletVisual &&
                (graph.MapEvent.IsFieldBattle || graph.MapEvent.IsSallyOut))
            {
                battleSizeCorrection.Register(gauntletVisual);
            }
        }
        catch (Exception ex)
        {
            // Visual failures must not leave an otherwise authoritative battle unregistered. The graph
            // remains complete and future teardown still calls OnMapEventEnd on the local visual.
            Logger.Error(ex, "Failed to initialize local visual for MapEvent {MapEventId}", message.MapEventId);
        }
    }

    private static bool IsVisibleToLocalPlayer(MapEvent mapEvent)
    {
        foreach (var party in mapEvent.InvolvedParties)
        {
            if (party?.IsVisible == true)
                return true;
        }

        return false;
    }

    private static bool Invalid(string messageTemplate, params object[] values)
    {
        Logger.Error(messageTemplate, values);
        return false;
    }

    private sealed class ResolvedGraph
    {
        public ResolvedGraph(
            Settlement settlement,
            ResolvedComponent component,
            ResolvedSide defenderSide,
            ResolvedSide attackerSide)
        {
            Settlement = settlement;
            Component = component;
            DefenderSide = defenderSide;
            AttackerSide = attackerSide;
        }

        public Settlement Settlement { get; }
        public ResolvedComponent Component { get; }
        public ResolvedSide DefenderSide { get; }
        public ResolvedSide AttackerSide { get; }
        public MapEvent MapEvent { get; set; }
        public TroopUpgradeTracker Tracker { get; set; }
        public IMapEventVisual Visual { get; set; }
    }

    private sealed class ResolvedComponent
    {
        public ResolvedComponent(
            MapEventComponentInitializationData data,
            Type componentType,
            Dictionary<ItemObject, float> raidProductionRewards)
        {
            Data = data;
            ComponentType = componentType;
            RaidProductionRewards = raidProductionRewards;
        }

        public MapEventComponentInitializationData Data { get; }
        public Type ComponentType { get; }
        public Dictionary<ItemObject, float> RaidProductionRewards { get; }
        public MapEventComponent Instance { get; set; }
    }

    private sealed class ResolvedSide
    {
        public ResolvedSide(
            MapEventSideInitializationData data,
            PartyBase leaderParty,
            IFaction faction,
            List<ResolvedParty> parties)
        {
            Data = data;
            LeaderParty = leaderParty;
            Faction = faction;
            Parties = parties;
        }

        public MapEventSideInitializationData Data { get; }
        public PartyBase LeaderParty { get; }
        public IFaction Faction { get; }
        public List<ResolvedParty> Parties { get; }
        public MapEventSide Instance { get; set; }
    }

    private sealed class ResolvedParty
    {
        public ResolvedParty(
            MapEventPartyInitializationData data,
            PartyBase partyBase,
            FlattenedTroopRoster flattenedRoster)
        {
            Data = data;
            PartyBase = partyBase;
            FlattenedRoster = flattenedRoster;
            OriginalMobilePartyPosition = partyBase.MobileParty?.Position ?? default;
            OriginalEventPositionAdder = partyBase.MobileParty?.EventPositionAdder ?? default;
        }

        public MapEventPartyInitializationData Data { get; }
        public PartyBase PartyBase { get; }
        public FlattenedTroopRoster FlattenedRoster { get; }
        public CampaignVec2 OriginalMobilePartyPosition { get; }
        public Vec2 OriginalEventPositionAdder { get; }
        public MapEventParty Instance { get; set; }
        public TroopRoster Wounded { get; set; }
        public TroopRoster Died { get; set; }
        public TroopRoster Routed { get; set; }
    }

    private sealed class ReferenceComparer<T> : IEqualityComparer<T>
        where T : class
    {
        public static readonly ReferenceComparer<T> Instance = new ReferenceComparer<T>();

        public bool Equals(T x, T y) => ReferenceEquals(x, y);

        public int GetHashCode(T obj) => RuntimeHelpers.GetHashCode(obj);
    }
}
