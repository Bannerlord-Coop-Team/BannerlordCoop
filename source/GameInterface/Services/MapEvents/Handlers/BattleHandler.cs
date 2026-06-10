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
using GameInterface.Services.MapEvents.Logging;
using GameInterface.Services.MapEvents.Messages;
using GameInterface.Services.MapEvents.Messages.Leave;
using GameInterface.Services.MapEvents.Messages.Start;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using LiteNetLib;
using Serilog;
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
        this.mapEventLogger = mapEventLogger;
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

        mapEventLogger.DebugMapEvent(mapEvent, "Handling network attack mission attempted for map event. Setting attack mission attempted to true");

        foreach(var side in mapEvent._sides)
        {
            side.MakeReadyForMission(null);
        }

        var message = new NetworkStartAttackMission();
        network.Send(payload.Who as NetPeer, message);
    }

    private void Handle_NetworkStartAttackMission(MessagePayload<NetworkStartAttackMission> payload)
    {
        var battle = PlayerEncounter.Battle;
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

        // TODO make this server side
        rec2.RandomTerrainSeed = MBRandom.RandomInt(10000);
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
        if (PlayerEncounter.Current != null)
        {
            // TODO determine force out of settlement
            PlayerEncounter.Finish(true);
        }
        
        GameMenu.ExitToLast();
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
            if (objectManager.TryGetIdWithLogging(addedParty, out var mapEventPartyId))
            {
                partyIds.Add(mapEventPartyId);
            }
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

        network.SendAll(new NetworkMapEventLockChanged(isBlocked, count));

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