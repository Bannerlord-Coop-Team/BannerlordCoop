using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using Common.Util;
using Coop.Core.Server.Services.Time.Messages;
using GameInterface.Services.GameDebug.Messages;
using GameInterface.Services.Heroes.Enum;
using GameInterface.Services.Heroes.Interaces;
using GameInterface.Services.Heroes.Messages;
using GameInterface.Services.MapEventParties;
using GameInterface.Services.MapEventParties.Messages;
using GameInterface.Services.MapEvents.Logging;
using GameInterface.Services.MapEvents.Messages;
using GameInterface.Services.MapEvents.Messages.Leave;
using GameInterface.Services.MapEvents.Messages.Start;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using LiteNetLib;
using Serilog;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.Map;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace GameInterface.Services.MapEvents.Handlers;

internal class BattleHandler : IHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger<BattleHandler>();

    private readonly IMessageBroker messageBroker;
    private readonly IObjectManager objectManager;
    private readonly INetwork network;
    private readonly IMapEventLogger mapEventLogger;
    private readonly IPlayerManager playerRegistry;
    private readonly ITimeControlInterface timeControlInterface;

    // Server-side: number of players in a map event at the last broadcast, used to
    // detect when fast-forward becomes (un)available and to keep clients informed.
    private int lastBroadcastPlayersInMapEvent;

    // Exclusive upper bound for the terrain seed, preserving the range of the original
    // client-side MBRandom.RandomInt(10000) roll this replaces.
    private const int MaxTerrainSeed = 10000;

    // Server-side: terrain seed chosen once per map event and reused for every client
    // that opens the same battle, so they all use the same terrain seed. Keyed by
    // map event id; the entry is evicted when the event finalizes.
    private readonly ConcurrentDictionary<string, int> mapEventTerrainSeeds = new ConcurrentDictionary<string, int>();
    private readonly Random terrainSeedRandom = new Random();

    public BattleHandler(
        IMessageBroker messageBroker,
        IObjectManager objectManager,
        INetwork network,
        IMapEventLogger mapEventLogger,
        IPlayerManager playerRegistry,
        ITimeControlInterface timeControlInterface)
    {
        this.messageBroker = messageBroker;
        this.objectManager = objectManager;
        this.network = network;
        this.mapEventLogger = mapEventLogger;
        this.playerRegistry = playerRegistry;
        this.timeControlInterface = timeControlInterface;
        messageBroker.Subscribe<PlayerJoinedBattle>(Handle_PlayerJoinedBattle);

        messageBroker.Subscribe<MapEventFinalizeAttempted>(Handle_MapEventFinalizeAttempted);
        messageBroker.Subscribe<NetworkMapEventFinalizeAttempted>(Handle_NetworkMapEventFinalizeAttempted);
        messageBroker.Subscribe<NetworkMapEventFinalized>(Handle_NetworkMapEventFinalized);

        messageBroker.Subscribe<MapEventInvolvedPartiesAdded>(Handle_MapEventInvolvedPartiesAdded);
        messageBroker.Subscribe<NetworkAddInvolvedParties>(Handle_NetworkAddInvolvedParties);

        messageBroker.Subscribe<AttackMissionAttempted>(Handle_AttackMissionAttempted);
        messageBroker.Subscribe<NetworkAttackMissionAttempted>(Handle_NetworkAttackMissionAttempted);
        messageBroker.Subscribe<NetworkStartAttackMission>(Handle_NetworkStartAttackMission);

        messageBroker.Subscribe<MapEventFinalized>(Handle_MapEventFinalized);
        messageBroker.Subscribe<TimeSpeedChangedAttempted>(Handle_TimeSpeedChangedAttempted);

        timeControlInterface.AddFastForwardPolicy(FastForwardWhilePlayerInMapEventPolicy);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<PlayerJoinedBattle>(Handle_PlayerJoinedBattle);

        messageBroker.Unsubscribe<MapEventFinalizeAttempted>(Handle_MapEventFinalizeAttempted);
        messageBroker.Unsubscribe<NetworkMapEventFinalizeAttempted>(Handle_NetworkMapEventFinalizeAttempted);
        messageBroker.Unsubscribe<NetworkMapEventFinalized>(Handle_NetworkMapEventFinalized);

        messageBroker.Unsubscribe<MapEventInvolvedPartiesAdded>(Handle_MapEventInvolvedPartiesAdded);
        messageBroker.Unsubscribe<NetworkAddInvolvedParties>(Handle_NetworkAddInvolvedParties);

        messageBroker.Unsubscribe<AttackMissionAttempted>(Handle_AttackMissionAttempted);
        messageBroker.Unsubscribe<NetworkAttackMissionAttempted>(Handle_NetworkAttackMissionAttempted);
        messageBroker.Unsubscribe<NetworkStartAttackMission>(Handle_NetworkStartAttackMission);

        messageBroker.Unsubscribe<MapEventFinalized>(Handle_MapEventFinalized);
        messageBroker.Unsubscribe<TimeSpeedChangedAttempted>(Handle_TimeSpeedChangedAttempted);

        timeControlInterface.RemoveFastForwardPolicy(FastForwardWhilePlayerInMapEventPolicy);
    }

    private void Handle_AttackMissionAttempted(MessagePayload<AttackMissionAttempted> payload)
    {
        if (!objectManager.TryGetIdWithLogging(payload.What.MapEvent, out string mapEventId))
            return;

        mapEventLogger.DebugMapEvent(payload.What.MapEvent, "Handling attack mission attempted for map event");

        var message = new NetworkAttackMissionAttempted(mapEventId);
        network.SendAll(message);
    }

    private void Handle_NetworkAttackMissionAttempted(MessagePayload<NetworkAttackMissionAttempted> payload)
    {
        if (!objectManager.TryGetObject(payload.What.MapEventId, out MapEvent mapEvent))
            return;

        mapEventLogger.DebugMapEvent(mapEvent, "Handling network attack mission attempted for map event. Making sides mission-ready and replying with mission start");

        foreach(var side in mapEvent._sides)
        {
            side.MakeReadyForMission(null);
        }

        // Roll the terrain seed once for this map event and reuse it for every client
        // that opens the battle, so they all use the same terrain seed. The seed is
        // chosen server-side and carried in the message instead of rolled per machine.
        var randomTerrainSeed = mapEventTerrainSeeds.GetOrAdd(payload.What.MapEventId, _ => RollTerrainSeed());

        var message = new NetworkStartAttackMission(randomTerrainSeed);
        network.Send(payload.Who as NetPeer, message);
    }

    private int RollTerrainSeed()
    {
        // This runs on the network thread, so it avoids MBRandom, which mutates the
        // game's shared main-thread RNG state. System.Random is not thread-safe, so
        // the shared instance is guarded.
        lock (terrainSeedRandom)
        {
            return terrainSeedRandom.Next(MaxTerrainSeed);
        }
    }

    private void Handle_NetworkStartAttackMission(MessagePayload<NetworkStartAttackMission> payload)
    {
        // Opening a mission pushes a screen, and ScreenManager only tolerates screen
        // changes from the main thread; doing it from the network thread races its
        // layer lists and crashes the game.
        var randomTerrainSeed = payload.What.RandomTerrainSeed;
        GameLoopRunner.RunOnMainThread(() => OpenAttackMission(randomTerrainSeed));
    }

    private static void OpenAttackMission(int randomTerrainSeed)
    {
        try
        {
            // The encounter can end (or another mission can open) between the server
            // round-trip and this running, so everything the mission depends on is
            // re-validated here rather than at message arrival.
            if (Campaign.Current == null)
            {
                Logger.Warning("Received {Message} but the campaign was not loaded, not opening battle mission", nameof(NetworkStartAttackMission));
                return;
            }

            var battle = PlayerEncounter.Battle;
            if (battle == null)
            {
                Logger.Warning("Received {Message} but PlayerEncounter.Battle was null, not opening battle mission", nameof(NetworkStartAttackMission));
                return;
            }

            // A finalized battle keeps PlayerEncounter.Battle set but releases the
            // main party from the map event, which the mission setup dereferences.
            if (MobileParty.MainParty?.MapEvent == null)
            {
                Logger.Warning("Received {Message} but the main party is no longer in a map event, not opening battle mission", nameof(NetworkStartAttackMission));
                return;
            }

            // Pressing attack again while the request is in flight produces a second
            // mission start; opening on top of the running mission corrupts the game
            // state stack. MissionState.Current is set synchronously by the state
            // push, unlike Mission.Current which is only set on the mission's first
            // tick, so it also covers two mission starts queued in the same frame.
            if (MissionState.Current != null)
            {
                Logger.Warning("Received {Message} but a mission is already open, not opening battle mission", nameof(NetworkStartAttackMission));
                return;
            }

            bool isNavalEncounter = PlayerEncounter.IsNavalEncounter();
            CampaignVec2 position = MobileParty.MainParty.Position;

            IMapScene mapSceneWrapper = Campaign.Current.MapSceneWrapper;
            MapPatchData mapPatchAtPosition = mapSceneWrapper.GetMapPatchAtPosition(position);


            string battleScene = Campaign.Current.Models.SceneModel.GetBattleSceneForMapPatch(mapPatchAtPosition, isNavalEncounter);
            MissionInitializerRecord rec2 = new MissionInitializerRecord(battleScene);
            TerrainType faceTerrainType2 = Campaign.Current.MapSceneWrapper.GetFaceTerrainType(MobileParty.MainParty.CurrentNavigationFace);
            rec2.TerrainType = (int)faceTerrainType2;
            rec2.DamageToFriendsMultiplier = Campaign.Current.Models.DifficultyModel.GetPlayerTroopsReceivedDamageMultiplier();
            rec2.DamageFromPlayerToFriendsMultiplier = Campaign.Current.Models.DifficultyModel.GetPlayerTroopsReceivedDamageMultiplier();
            rec2.NeedsRandomTerrain = false;
            rec2.PlayingInCampaignMode = true;

            // Seed chosen server-side and carried in NetworkStartAttackMission so every
            // client uses the same terrain seed for this battle.
            rec2.RandomTerrainSeed = randomTerrainSeed;
            rec2.AtmosphereOnCampaign = Campaign.Current.Models.MapWeatherModel.GetAtmosphereModel(MobileParty.MainParty.Position);
            rec2.SceneHasMapPatch = true;
            rec2.DecalAtlasGroup = 2;
            rec2.PatchCoordinates = mapPatchAtPosition.normalizedCoordinates;
            position = battle.AttackerSide.LeaderParty.Position;
            Vec2 v2 = position.ToVec2();
            position = battle.DefenderSide.LeaderParty.Position;
            rec2.PatchEncounterDir = (v2 - position.ToVec2()).Normalized();

            CampaignMission.OpenBattleMission(rec2);
        }
        catch (Exception e)
        {
            // GameLoopRunner runs queued actions unguarded, so a throw from here
            // would escape into the game's main tick and crash it.
            Logger.Error(e, "Failed to open the battle mission for {Message}", nameof(NetworkStartAttackMission));
        }
    }

    private void Handle_MapEventFinalizeAttempted(MessagePayload<MapEventFinalizeAttempted> payload)
    {

        if (!objectManager.TryGetIdWithLogging(payload.What.MapEvent, out string mapEventId))
            return;

        if (MapEventConfig.Debug)
            mapEventLogger.DebugMapEvent(payload.What.MapEvent, "Map event finalize attempted. Sending network message to finalize map event on all clients.");

        var message = new NetworkMapEventFinalizeAttempted(mapEventId);
        network.SendAll(message);
    }
    private void Handle_NetworkMapEventFinalizeAttempted(MessagePayload<NetworkMapEventFinalizeAttempted> payload)
    {
        if (!objectManager.TryGetObjectWithLogging(payload.What.MapEventId, out MapEvent mapEvent))
            return;

        if (MapEventConfig.Debug)
            mapEventLogger.DebugMapEvent(mapEvent, "Handling network map event finalize attempted. Finalizing map event.");

        GameLoopRunner.RunOnMainThread(() =>
        {
            mapEvent.FinalizeEventAux();
        }, blocking: true);
        

        var message = new NetworkMapEventFinalized();
        network.Send(payload.Who as NetPeer, message);
    }

    private void Handle_NetworkMapEventFinalized(MessagePayload<NetworkMapEventFinalized> payload)
    {
        GameLoopRunner.RunOnMainThread(() =>
        {
            if (Campaign.Current == null) return;

            // When this battle ended with the local player captured, the captivity flow owns the UI:
            // PlayerCaptivityClientHandler has switched to the prisoner menu and leaves the encounter
            // itself. Exiting menus here would close the capture screen.
            if (PlayerCaptivity.IsCaptive) return;

            if (PlayerEncounter.Current != null)
            {
                // TODO determine force out of settlement
                PlayerEncounter.Finish(true);
            }

            GameMenu.ExitToLast();
        });
    }

    private void Handle_MapEventInvolvedPartiesAdded(MessagePayload<MapEventInvolvedPartiesAdded> payload)
    {
        // This message is published when a player party joins a battle (a fresh one
        // during MapEvent.Initialize, or an already-running one as a reinforcement);
        // the joining party's MapEventSide is already set by this point, so the live
        // count is accurate.
        CapFastForwardForMapEvent();
        RefreshFastForwardState();

        var message = payload.What;
        if (!objectManager.TryGetIdWithLogging(message.MapEvent, out var mapEventSideId))
            return;

        mapEventLogger.DebugMapEvent(message.MapEvent, "Map event involved parties added. Added party count: {AddedPartyCount}", message.AddedParties.Count());

        var partyIds = new List<string>();

        foreach (var addedParty in message.AddedParties)
        {
            if (!objectManager.TryGetIdWithLogging(addedParty, out var mapEventPartyId))
                continue;

            partyIds.Add(mapEventPartyId);

            // A player just created or joined this map event, so push every involved party's
            // flattened roster to clients (AI-only battles never reach here). Clients need these to
            // spawn troops in the mission; in-progress AI parties already have a roster built from
            // server simulation. Per-troop changes after this are kept in sync incrementally.
            if (addedParty._roster == null)
                continue;

            var flattenedTroops = FlattenedTroopSerializer.Serialize(addedParty._roster, objectManager);
            network.SendAll(new NetworkUpdateMapEventParty(mapEventPartyId, flattenedTroops));
        }

        network.SendAll(new NetworkAddInvolvedParties(
            mapEventSideId,
            partyIds.ToArray()
        ));
    }

    private void Handle_NetworkAddInvolvedParties(MessagePayload<NetworkAddInvolvedParties> payload)
    {
        var message = payload.What;

        if (!objectManager.TryGetObjectWithLogging<MapEvent>(message.MapEventId, out var mapEvent))
            return;

        mapEventLogger.DebugMapEvent(mapEvent, "Handling network add involved parties. Party count: {MapEventPartyCount}", message.MapEventPartyIds.Length);

        using (new AllowedThread())
        {
            foreach (var mapEventPartyId in message.MapEventPartyIds)
            {
                if (!objectManager.TryGetObjectWithLogging<MapEventParty>(mapEventPartyId, out var mapEventParty))
                    continue;

                mapEventLogger.DebugMapEvent(mapEvent, "Adding involved map event party {MapEventPartyId} to troop upgrade tracker", mapEventPartyId);
                mapEvent.TroopUpgradeTracker.AddParty(mapEventParty);
            }
        }
    }

    private void Handle_PlayerJoinedBattle(MessagePayload<PlayerJoinedBattle> payload)
    {
        if (AllPlayersInMapEvents())
        {
            timeControlInterface.ServerSetTimeControl(TimeControlEnum.Pause);
        }
        else
        {
            // A player started a battle while others remain on the map.
            CapFastForwardForMapEvent();
        }

        RefreshFastForwardState();
    }

    private void Handle_MapEventFinalized(MessagePayload<MapEventFinalized> payload)
    {
        // A map event ended; its parties have left it, so re-evaluate whether
        // fast-forward should become available again.
        RefreshFastForwardState(finalizedMapEvent: payload.What.MapEvent);

        // The battle is over, so drop its cached terrain seed.
        if (objectManager.TryGetId(payload.What.MapEvent, out var mapEventId))
            mapEventTerrainSeeds.TryRemove(mapEventId, out _);
    }

    private void Handle_TimeSpeedChangedAttempted(MessagePayload<TimeSpeedChangedAttempted> payload)
    {
        // Notify the host (clients are notified by their own time handler) when they
        // try to fast-forward while it is blocked by a map event.
        if (ModInformation.IsClient)
            return;

        if (payload.What.NewControlMode != TimeControlEnum.Play_2x)
            return;

        var playersInMapEvent = CountPlayersInMapEvents();
        if (playersInMapEvent == 0)
            return;

        messageBroker.Publish(this, new SendInformationMessage(
            MapEventTimeControlMessages.FastForwardBlocked(playersInMapEvent)));
    }

    /// <summary>
    /// Recomputes how many players are in a map event and, when that crosses the
    /// 0 / non-0 boundary, announces it locally and informs clients. Server only.
    /// </summary>
    /// <param name="finalizedMapEvent">A map event being finalized, excluded from the count.</param>
    private void RefreshFastForwardState(MapEvent finalizedMapEvent = null)
    {
        if (ModInformation.IsClient)
            return;

        var count = CountPlayersInMapEvents(finalizedMapEvent);
        if (count == lastBroadcastPlayersInMapEvent)
            return;

        var wasBlocked = lastBroadcastPlayersInMapEvent > 0;
        var isBlocked = count > 0;
        lastBroadcastPlayersInMapEvent = count;

        network.SendAll(new NetworkMapEventLockChanged(count));

        if (isBlocked && !wasBlocked)
            messageBroker.Publish(this, new SendInformationMessage(MapEventTimeControlMessages.FastForwardDisabled));
        else if (!isBlocked && wasBlocked)
            messageBroker.Publish(this, new SendInformationMessage(MapEventTimeControlMessages.FastForwardEnabled));
    }

    /// <summary>
    /// Drops the campaign out of fast-forward when a player is in a map event. The
    /// fast-forward policy then keeps it capped at normal speed until every player
    /// has left their map event. Runs on the server only.
    /// </summary>
    private void CapFastForwardForMapEvent()
    {
        if (ModInformation.IsClient)
            return;

        if (timeControlInterface.GetTimeControl() == TimeControlEnum.Play_2x)
        {
            timeControlInterface.ServerSetTimeControl(TimeControlEnum.Play_1x);
        }
    }

    /// <summary>
    /// Fast-forwarding the campaign map is not allowed while any player is in a map event.
    /// </summary>
    /// <returns>True if fast-forwarding is allowed, otherwise false</returns>
    private bool FastForwardWhilePlayerInMapEventPolicy()
    {
        return AnyPlayerInMapEvent() == false;
    }

    private bool AllPlayersInMapEvents()
    {
        return playerRegistry.Players.All(player =>
        {
            if (!objectManager.TryGetObjectWithLogging<MobileParty>(player.MobilePartyId, out var playerParty))
                return false;

            return playerParty.MapEvent != null;
        });
    }

    private bool AnyPlayerInMapEvent() => CountPlayersInMapEvents() > 0;

    private int CountPlayersInMapEvents(MapEvent excluding = null)
    {
        // Backs the fast-forward policy and messaging, which are evaluated on every
        // time-control change, so this uses the non-logging lookup to avoid spamming
        // the log when a party is momentarily unresolved.
        return playerRegistry.Players.Count(player =>
        {
            if (!objectManager.TryGetObject<MobileParty>(player.MobilePartyId, out var playerParty))
                return false;

            return playerParty.MapEvent != null && playerParty.MapEvent != excluding;
        });
    }
}