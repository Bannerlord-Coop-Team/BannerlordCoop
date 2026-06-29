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
using GameInterface.Services.MobileParties.Extensions;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using LiteNetLib;
using Serilog;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
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

    // The -10 player-hostility relation penalty vanilla applies against the target faction leader
    // when an attack first turns a faction hostile (the war block of BeHostileAction).
    private const int PlayerHostilityRelationPenalty = -10;

    // Server-side: terrain seed chosen once per map event and reused for every client
    // that opens the same battle, so they all use the same terrain seed. Keyed by
    // map event id; the entry is evicted when the event finalizes.
    private readonly ConcurrentDictionary<string, int> mapEventTerrainSeeds = new ConcurrentDictionary<string, int>();
    private readonly Random terrainSeedRandom = new Random();

    // Server-side: map events whose finalize has already run, so a duplicate finalize is ignored. A battle can
    // be finalized twice: the host leaves (finalize #1), host migration promotes another player, and that new
    // host's own "done" sends finalize #2. Re-running MapEvent.FinalizeEventAux re-forfeits the rosters (the same
    // troop removed twice -> the client roster goes negative). Keyed by the event INSTANCE via a weak table, so
    // it self-evicts when the event is GC'd (no growth, no eviction race vs. a duplicate that arrives a second
    // later) and never conflates two distinct events that happen to share an object id.
    private static readonly object FinalizedMarker = new object();
    private readonly ConditionalWeakTable<MapEvent, object> finalizedMapEvents = new ConditionalWeakTable<MapEvent, object>();
    private readonly object finalizedMapEventsLock = new object();

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
        messageBroker.Subscribe<MapEventConcluded>(Handle_MapEventConcluded);

        messageBroker.Subscribe<MapEventInvolvedPartiesAdded>(Handle_MapEventInvolvedPartiesAdded);
        messageBroker.Subscribe<NetworkAddInvolvedParties>(Handle_NetworkAddInvolvedParties);

        messageBroker.Subscribe<NetworkBattleStartRequest>(Handle_NetworkBattleStartRequest);
        messageBroker.Subscribe<NetworkStartAttackMission>(Handle_NetworkStartAttackMission);
        messageBroker.Subscribe<NetworkBattleModeSet>(Handle_NetworkBattleModeSet);

        messageBroker.Subscribe<MapEventFinalized>(Handle_MapEventFinalized);
        messageBroker.Subscribe<TimeSpeedChangedAttempted>(Handle_TimeSpeedChangedAttempted);

        messageBroker.Subscribe<PlayerJoinBattleAttempted>(Handle_PlayerJoinBattleAttempted);
        messageBroker.Subscribe<NetworkRequestJoinBattle>(Handle_NetworkRequestJoinBattle);

        messageBroker.Subscribe<PlayerLeaveBattleAttempted>(Handle_PlayerLeaveBattleAttempted);
        messageBroker.Subscribe<NetworkRequestLeaveBattle>(Handle_NetworkRequestLeaveBattle);
        messageBroker.Subscribe<NetworkPartyLeftBattle>(Handle_NetworkPartyLeftBattle);

        timeControlInterface.AddFastForwardPolicy(FastForwardWhilePlayerInMapEventPolicy);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<PlayerJoinedBattle>(Handle_PlayerJoinedBattle);

        messageBroker.Unsubscribe<MapEventFinalizeAttempted>(Handle_MapEventFinalizeAttempted);
        messageBroker.Unsubscribe<NetworkMapEventFinalizeAttempted>(Handle_NetworkMapEventFinalizeAttempted);
        messageBroker.Unsubscribe<NetworkMapEventFinalized>(Handle_NetworkMapEventFinalized);
        messageBroker.Unsubscribe<MapEventConcluded>(Handle_MapEventConcluded);

        messageBroker.Unsubscribe<MapEventInvolvedPartiesAdded>(Handle_MapEventInvolvedPartiesAdded);
        messageBroker.Unsubscribe<NetworkAddInvolvedParties>(Handle_NetworkAddInvolvedParties);

        messageBroker.Unsubscribe<NetworkBattleStartRequest>(Handle_NetworkBattleStartRequest);
        messageBroker.Unsubscribe<NetworkStartAttackMission>(Handle_NetworkStartAttackMission);
        messageBroker.Unsubscribe<NetworkBattleModeSet>(Handle_NetworkBattleModeSet);

        messageBroker.Unsubscribe<MapEventFinalized>(Handle_MapEventFinalized);
        messageBroker.Unsubscribe<TimeSpeedChangedAttempted>(Handle_TimeSpeedChangedAttempted);

        messageBroker.Unsubscribe<PlayerJoinBattleAttempted>(Handle_PlayerJoinBattleAttempted);
        messageBroker.Unsubscribe<NetworkRequestJoinBattle>(Handle_NetworkRequestJoinBattle);

        messageBroker.Unsubscribe<PlayerLeaveBattleAttempted>(Handle_PlayerLeaveBattleAttempted);
        messageBroker.Unsubscribe<NetworkRequestLeaveBattle>(Handle_NetworkRequestLeaveBattle);
        messageBroker.Unsubscribe<NetworkPartyLeftBattle>(Handle_NetworkPartyLeftBattle);

        timeControlInterface.RemoveFastForwardPolicy(FastForwardWhilePlayerInMapEventPolicy);
    }

    /// <summary>[Server] Handle a battle-start request for the live-mission mode: gate it, make the sides
    /// mission-ready, send the requester the mission start, and reply. Requests for other modes are ignored here.</summary>
    private void Handle_NetworkBattleStartRequest(MessagePayload<NetworkBattleStartRequest> payload)
    {
        if (!ModInformation.IsServer)
            return;

        if (payload.What.Mode != (int)BattleStartMode.Mission)
            return;

        if (!objectManager.TryGetObject(payload.What.MapEventId, out MapEvent _))
            return;

        // Roll the terrain seed once for this map event and reuse it for every client
        // that opens the battle, so they all use the same terrain seed. The seed is
        // chosen server-side and carried in the message instead of rolled per machine.
        var randomTerrainSeed = mapEventTerrainSeeds.GetOrAdd(payload.What.MapEventId, _ => RollTerrainSeed());
        var requester = payload.Who as NetPeer;

        // _sides is game state the main-thread tick also touches; mutating it from the
        // network thread races the tick. Make the sides mission-ready on the main thread.
        // Re-resolve the event at drain time: it may have finalized between this request
        // arriving and the queued action running, in which case a captured reference would
        // point at a torn-down event.
        GameThread.Run(() =>
        {
            try
            {
                if (!objectManager.TryGetObject(payload.What.MapEventId, out MapEvent mapEvent))
                    return;

                // Server-authoritative mode gate: accept the live mission only if no auto-resolve simulation already
                // owns this event. On reject, don't make the sides mission-ready or reply — the requesting client
                // waits for NetworkStartAttackMission to open the mission, so it simply stays at the encounter menu.
                if (!ServerBattleModeArbiter.TryClaimMission(payload.What.MapEventId))
                {
                    mapEventLogger.DebugMapEvent(mapEvent, "Rejecting attack mission: an auto-resolve simulation is already underway for this event");
                    network.Send(requester, new NetworkBattleStartReply(payload.What.RequestId, false));
                    return;
                }

                mapEventLogger.DebugMapEvent(mapEvent, "Handling network attack mission attempted for map event. Making sides mission-ready and replying with mission start");

                // Apply the diplomatic consequences of the client's attack (war / relation)
                // authoritatively before the mission opens, reproducing the hostile-action head of
                // vanilla EncounterAttackConsequence that neither the client nor the server runs.
                ApplyClientAttackHostileConsequences(mapEvent, payload.What.AttackerPartyId);

                foreach (var side in mapEvent._sides)
                {
                    side.MakeReadyForMission(null);
                }

                // Reply first so the requesting client's blocked consequence unblocks before the mission-open
                // message arrives — the mission then opens off the menu-consequence stack, as in the pre-coordinator
                // flow, rather than re-entrantly during the blocking wait.
                network.Send(requester, new NetworkBattleStartReply(payload.What.RequestId, true));

                network.Send(requester, new NetworkStartAttackMission(randomTerrainSeed));

                // Claim the event for the mission mode on every client, so one still sitting at the encounter menu
                // greys out the auto-resolve option — a map event is fought as a live mission XOR an auto-resolve,
                // never both (see BattleModeEncounterOptionsPatch / BattleModeRegistry).
                network.SendAll(new NetworkBattleModeSet(payload.What.MapEventId, (int)BattleStartMode.Mission));
            }
            catch (Exception e)
            {
                Logger.Error(e, "Failed to make map event sides mission-ready for {Message}", nameof(NetworkBattleStartRequest));
                network.Send(requester, new NetworkBattleStartReply(payload.What.RequestId, false));
            }
        });
    }

    /// <summary>
    /// [Client] The server claimed a map event for a battle-resolution mode. If the local player's own party is in
    /// that event, record the mode (<see cref="BattleModeRegistry"/>) so the encounter menu greys out the wrong-mode
    /// options (<see cref="Patches.BattleModeEncounterOptionsPatch"/>). Clients not in the event ignore it.
    /// </summary>
    private void Handle_NetworkBattleModeSet(MessagePayload<NetworkBattleModeSet> payload)
    {
        if (!ModInformation.IsClient) return;

        var mapEventId = payload.What.MapEventId;
        var mode = (BattleStartMode)payload.What.Mode;

        GameThread.Run(() =>
        {
            if (!objectManager.TryGetObject(mapEventId, out MapEvent mapEvent) || mapEvent == null)
                return;

            if (MobileParty.MainParty?.MapEvent != mapEvent && PlayerEncounter.Battle != mapEvent)
                return;

            BattleModeRegistry.Begin(mapEventId, mode);
        });
    }

    /// <summary>
    /// When a client attacks a not-already-hostile party, declares war on the target faction and
    /// applies the player-hostility relation penalty against its leader — the war block of vanilla
    /// MenuHelper.EncounterAttackConsequence (BeHostileAction.ApplyEncounterHostileAction), which
    /// neither the client (it defers to the server) nor the dedicated server (it never opens the
    /// encounter menu) runs. Vanilla gates the war declaration on the attacker being the
    /// single-player main party; the server is never that party, so it is reproduced explicitly for
    /// the client attacker. Runs with patches live: the war declaration replicates through the
    /// server-authoritative stance sync, and the relation change is applied server-authoritatively
    /// like every other co-op relation change. The whole block sits behind the not-already-at-war
    /// check, so repeated attack requests for the same battle apply it at most once.
    /// </summary>
    private void ApplyClientAttackHostileConsequences(MapEvent mapEvent, string attackerPartyId)
    {
        try
        {
            // GameThread runs queued actions unguarded, so the campaign can have been torn down
            // between the request arriving and this draining; the sibling OpenAttackMission guards
            // the same way before dereferencing campaign state.
            if (Campaign.Current == null)
                return;

            if (!objectManager.TryGetObject(attackerPartyId, out MobileParty attackerMobileParty))
            {
                Logger.Warning("Could not resolve attacker party {AttackerPartyId} for attack hostile-action consequences", attackerPartyId);
                return;
            }

            var attackerParty = attackerMobileParty.Party;

            // OpponentSide is only meaningful while the attacker is a side in this event.
            if (attackerParty.MapEvent != mapEvent)
                return;

            var defenderParty = mapEvent.GetLeaderParty(attackerParty.OpponentSide);
            if (defenderParty == null)
                return;

            var attackerFaction = attackerParty.MapFaction;
            var defenderFaction = defenderParty.MapFaction;
            if (attackerFaction == null || defenderFaction == null || attackerFaction == defenderFaction)
                return;

            if (Campaign.Current.Models.EncounterModel.IsEncounterExemptFromHostileActions(attackerParty, defenderParty))
                return;

            // Already hostile: nothing to declare, and this is what makes the consequence idempotent
            // across the duplicate attack requests a client can send during the server round-trip.
            if (FactionManager.IsAtWarAgainstFaction(attackerFaction, defenderFaction))
                return;

            if (attackerParty.LeaderHero != null && defenderFaction.Leader != null)
            {
                ChangeRelationAction.ApplyRelationChangeBetweenHeroes(attackerParty.LeaderHero, defenderFaction.Leader, PlayerHostilityRelationPenalty);
            }

            DeclareWarAction.ApplyByPlayerHostility(attackerFaction, defenderFaction);
        }
        catch (Exception e)
        {
            Logger.Error(e, "Failed to apply attack hostile-action consequences");
        }
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
        GameThread.Run(() => OpenAttackMission(randomTerrainSeed));
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

            // Engage the spawn gate BEFORE OpenBattleMission builds the mission — the deployment controller
            // spawns the initial wave during mission setup (inside OpenBattleMission), earlier than the
            // CoopBattleController attach. The host is computed locally (deterministic lowest controller id);
            // the server's authoritative assignment reconciles the gate later (BattleHostHandler).
            if (BattleSpawnConfig.Enabled
                && ContainerProvider.TryResolve(out IObjectManager battleObjectManager)
                && battleObjectManager.TryGetId(battle, out var battleMapEventId))
            {
                var isLocalHost = BattleHostElection.IsLocalHost(battle);
                BattleSpawnGate.BeginBattle(battleMapEventId, isLocalHost);
                Logger.Information("[BattleSync] Engaged spawn gate in OpenAttackMission: mapEvent={MapEventId} isHost={IsHost}", battleMapEventId, isLocalHost);
            }

            // Coop opens a custom field-battle mission (per-client troop suppliers, no deployment phase) instead
            // of the native one; the launcher lives in Missions and is resolved from the container. Fall back to
            // the native mission only if it is somehow unavailable, so a misconfiguration still yields a battle.
            if (ContainerProvider.TryResolve(out ICoopFieldBattleLauncher battleLauncher))
            {
                battleLauncher.OpenCoopFieldBattle(rec2);
            }
            else
            {
                Logger.Warning("[BattleSync] ICoopFieldBattleLauncher unavailable; opening native battle mission");
                CampaignMission.OpenBattleMission(rec2);
            }
        }
        catch (Exception e)
        {
            // GameThread runs queued actions unguarded, so a throw from here
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

        var playerPartyIds = FinalizeAndCollectPlayers(mapEvent);

        network.Send(payload.Who as NetPeer, new NetworkMapEventFinalized());

        // PvP (more than one player party): tell every involved player party to close its encounter menu.
        if (playerPartyIds.Length > 1)
            network.SendAll(new NetworkClosePvpEncounter(playerPartyIds));
    }

    /// <summary>
    /// [Server] A battle reached a victory state — finalize it and close EVERY involved player's encounter, so a
    /// concluded coop battle tears down without the player leaving the post-battle menu (the auto-finalize on
    /// conclusion). The explicit-leave path above keeps its single-player <c>NetworkMapEventFinalized</c> reply;
    /// here there is no leaver, so the close instruction covers all involved players directly.
    /// </summary>
    private void Handle_MapEventConcluded(MessagePayload<MapEventConcluded> payload)
    {
        if (!ModInformation.IsServer) return;

        if (!objectManager.TryGetObjectWithLogging(payload.What.MapEventId, out MapEvent mapEvent))
            return;

        if (MapEventConfig.Debug)
            mapEventLogger.DebugMapEvent(mapEvent, "Battle concluded; auto-finalizing and closing every involved player's encounter.");

        // Fires automatically on every victory, so guard the game thread: a finalize edge case must not escape
        // and tear down the campaign tick.
        try
        {
            var playerPartyIds = FinalizeAndCollectPlayers(mapEvent);

            // TEMP [UiDiag]: confirm the auto-finalize targets every winning player's party for the encounter
            // close (a winner missing here would never be told to close). Remove once the stuck-screen is fixed.
            Logger.Information("[UiDiag] MapEventConcluded {Id}: closing encounter for {Count} player party id(s): [{Ids}]",
                payload.What.MapEventId, playerPartyIds.Length, string.Join(",", playerPartyIds));

            if (playerPartyIds.Length > 0)
                network.SendAll(new NetworkClosePvpEncounter(playerPartyIds));
        }
        catch (Exception e)
        {
            Logger.Error(e, "Failed to auto-finalize concluded map event");
        }
    }

    /// <summary>
    /// [Server] Finalize <paramref name="mapEvent"/> on the game thread, capturing the involved player party ids
    /// first (finalize clears them) so they get a reliable server-addressed encounter close instead of each
    /// racing its own local teardown. <see cref="GameThread.Run"/> runs inline when already on the game thread.
    /// </summary>
    private string[] FinalizeAndCollectPlayers(MapEvent mapEvent)
    {
        // Finalize each map event at most once (see finalizedMapEvents). A duplicate finalize - most often a
        // post-migration second "done", where the host leaves, migration promotes another player, and that new
        // host's leave sends a second finalize - must not re-run FinalizeEventAux and re-forfeit the rosters.
        lock (finalizedMapEventsLock)
        {
            if (finalizedMapEvents.TryGetValue(mapEvent, out _))
            {
                objectManager.TryGetId(mapEvent, out var duplicateId);
                Logger.Warning("Ignoring duplicate finalize for already-finalized map event {MapEventId} (likely a post-migration second leave); not re-running the capture/roster forfeit.", duplicateId);
                return Array.Empty<string>();
            }
            finalizedMapEvents.Add(mapEvent, FinalizedMarker);
        }

        // The battle is over — release the mode claim so a later, unrelated battle on this event starts unclaimed.
        if (objectManager.TryGetId(mapEvent, out var mapEventIdForRelease))
            ServerBattleModeArbiter.Release(mapEventIdForRelease);

        string[] playerPartyIds = null;
        GameThread.Run(() =>
        {
            playerPartyIds = CollectPlayerPartyIds(mapEvent);
            mapEvent.FinalizeEventAux();
        }, blocking: true);
        return playerPartyIds ?? Array.Empty<string>();
    }

    /// <summary>[Server] Ids of the player parties on both sides of the event, captured before finalize clears them.</summary>
    private string[] CollectPlayerPartyIds(MapEvent mapEvent)
    {
        var ids = new List<string>();
        if (mapEvent?.AttackerSide == null || mapEvent.DefenderSide == null) return ids.ToArray();

        foreach (var party in mapEvent.InvolvedParties)
        {
            if (party?.MobileParty?.IsPlayerParty() == true && objectManager.TryGetId(party, out var id))
                ids.Add(id);
        }

        return ids.ToArray();
    }

    private void Handle_NetworkMapEventFinalized(MessagePayload<NetworkMapEventFinalized> payload)
    {
        GameThread.Run(() =>
        {
            if (Campaign.Current == null) return;

            // The local player's battle has ended — clear the recorded mode so the encounter-menu gate
            // (BattleModeEncounterOptionsPatch) no longer treats this event as claimed.
            BattleModeRegistry.End();

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
        var partyPositions = new List<CampaignVec2>();

        foreach (var addedParty in message.AddedParties)
        {
            if (!objectManager.TryGetIdWithLogging(addedParty, out var mapEventPartyId))
                continue;

            partyIds.Add(mapEventPartyId);
            // Capture the party's authoritative map position, in lockstep with the id and
            // before the roster check below so the two arrays stay index-aligned. Settlement
            // parties have no MobileParty; their slot is a default the client never applies.
            partyPositions.Add(addedParty.Party.MobileParty?.Position ?? default);

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
            partyIds.ToArray(),
            partyPositions.ToArray()
        ));

        // Tell any player parties just added to the battle to drop their "hold on" PvP popup — the battle menu
        // blocks them now. Server-driven because the client-side MapEventInvolvedPartiesAdded never fires for a
        // synced add (the client's own add is intercepted and routed to the server).
        var playerPartyIds = new List<string>();
        foreach (var addedParty in message.AddedParties)
        {
            if (addedParty.Party?.MobileParty?.IsPlayerParty() == true && objectManager.TryGetId(addedParty.Party, out var playerPartyId))
                playerPartyIds.Add(playerPartyId);
        }

        if (playerPartyIds.Count > 0)
            network.SendAll(new NetworkHidePvpPopup(playerPartyIds.ToArray()));

        // The conversation is over once the battle map event forms, so release the PvP interaction block. Holding it
        // longer re-blocks the parties (and hangs the encounter menu) when they later leave the map event; the map
        // event itself keeps others out while the battle runs.
        foreach (var id in playerPartyIds)
            ConversationPartyTracker.Instance?.EndPvpConversation(id);
    }

    private void Handle_NetworkAddInvolvedParties(MessagePayload<NetworkAddInvolvedParties> payload)
    {
        var message = payload.What;

        GameThread.Run(() =>
        {
            try
            {
                // The campaign can tear down (exit to menu, disconnect, save load) between
                // enqueuing this and the main thread draining it; bail before touching
                // campaign state (the position snap below dereferences Campaign.Current).
                if (Campaign.Current == null)
                    return;

                if (!objectManager.TryGetObjectWithLogging<MapEvent>(message.MapEventId, out var mapEvent))
                    return;

                mapEventLogger.DebugMapEvent(mapEvent, "Handling network add involved parties. Party count: {MapEventPartyCount}", message.MapEventPartyIds.Length);

                var positions = message.Positions;

                // Re-applying campaign-collection state replicated from the server; the
                // AutoSync TroopUpgradeTracker patches must stand down during the apply.
                using (new AllowedThread())
                {
                    for (int i = 0; i < message.MapEventPartyIds.Length; i++)
                    {
                        var mapEventPartyId = message.MapEventPartyIds[i];
                        if (!objectManager.TryGetObjectWithLogging<MapEventParty>(mapEventPartyId, out var mapEventParty))
                            continue;

                        mapEventLogger.DebugMapEvent(mapEvent, "Adding involved map event party {MapEventPartyId} to troop upgrade tracker", mapEventPartyId);
                        mapEvent.TroopUpgradeTracker.AddParty(mapEventParty);

                        // Snap the party to its server-side map position so it lines up with the
                        // battle. Every involved party is snapped, including this client's own, so
                        // all clients place the parties where the server has them, lined up with the
                        // battle center the server is authoritative for.
                        var mobileParty = mapEventParty.Party.MobileParty;
                        if (mobileParty != null && positions != null && i < positions.Length)
                        {
                            mobileParty.Position = positions[i];
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Logger.Error(e, "Failed to apply {Message}", nameof(NetworkAddInvolvedParties));
            }
        });
    }

    /// <summary>[Client] Bridge the local player's battle join to a server request.</summary>
    private void Handle_PlayerJoinBattleAttempted(MessagePayload<PlayerJoinBattleAttempted> payload)
    {
        var data = payload.What;

        if (!objectManager.TryGetIdWithLogging(data.MapEvent, out var mapEventId)) return;
        if (!objectManager.TryGetIdWithLogging(data.JoiningParty, out var partyId)) return;

        mapEventLogger.DebugMapEvent(data.MapEvent, "Requesting server to join battle. PartyId={PartyId}, Side={Side}", partyId, data.Side);

        // On a client, SendAll targets the server (its only connected peer).
        network.SendAll(new NetworkRequestJoinBattle(mapEventId, partyId, data.Side));
    }

    /// <summary>[Server] Perform the authoritative join; the native add replicates to all clients.</summary>
    private void Handle_NetworkRequestJoinBattle(MessagePayload<NetworkRequestJoinBattle> payload)
    {
        if (!ModInformation.IsServer) return;

        var data = payload.What;

        GameThread.Run(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<MapEvent>(data.MapEventId, out var mapEvent)) return;
            if (!objectManager.TryGetObjectWithLogging<PartyBase>(data.PartyId, out var party)) return;

            if (party.MapEventSide != null)
            {
                Logger.Warning("Ignoring join request: party {PartyId} is already in a map event", data.PartyId);
                return;
            }

            // The setter runs the native MapEventSide.AddPartyInternal on the server (NOT under AllowedThread), so the
            // AddIntercept publishes the battle-party add and it replicates to every client through the map-event sync.
            party.MapEventSide = mapEvent.GetMapEventSide(data.Side);

            // If this battle is being auto-resolved, pull the joiner into the simulation instead of leaving it stuck in
            // the encounter menu. A ForwardingBattleObserver on the event means a server-driven simulation is running.
            // Sent after the add above so the joiner applies the replicated battle-party add (and so builds its own
            // party into its scoreboard) before this open arrives; the simulation handler then opens it as a spectator.
            if (mapEvent.BattleObserver is ForwardingBattleObserver)
                network.SendAll(new NetworkOpenBattleSimulation(data.MapEventId));
        });
    }

    /// <summary>[Client] Bridge a joiner's leave to a server request; [Server/host] perform it directly.</summary>
    private void Handle_PlayerLeaveBattleAttempted(MessagePayload<PlayerLeaveBattleAttempted> payload)
    {
        if (!objectManager.TryGetIdWithLogging(payload.What.LeavingParty, out var partyId)) return;

        if (ModInformation.IsServer)
            RemovePartyFromBattleAndBroadcast(partyId);
        else
            network.SendAll(new NetworkRequestLeaveBattle(partyId));
    }

    /// <summary>[Server] A client asked to leave a battle without ending it.</summary>
    private void Handle_NetworkRequestLeaveBattle(MessagePayload<NetworkRequestLeaveBattle> payload)
    {
        if (!ModInformation.IsServer) return;

        RemovePartyFromBattleAndBroadcast(payload.What.PartyId);
    }

    // Single-party removal does not auto-replicate (RemovePartyInternal uses RemoveAt, bypassing the
    // collection sync), so remove authoritatively and broadcast the removal explicitly.
    private void RemovePartyFromBattleAndBroadcast(string partyId)
    {
        GameThread.Run(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<PartyBase>(partyId, out var party)) return;

            ApplyLeave(party);
            network.SendAll(new NetworkPartyLeftBattle(partyId));
        });
    }

    /// <summary>[Client] Apply a joiner's removal from its map event side.</summary>
    private void Handle_NetworkPartyLeftBattle(MessagePayload<NetworkPartyLeftBattle> payload)
    {
        var partyId = payload.What.PartyId;

        GameThread.Run(() =>
        {
            if (Campaign.Current == null) return;
            if (!objectManager.TryGetObjectWithLogging<PartyBase>(partyId, out var party)) return;

            ApplyLeave(party);
        });
    }

    // Remove the party from its side (idempotent) and, if it is this instance's own party, close its encounter UI.
    // PlayerEncounter.Finish is safe here: with MapEventSide already cleared, LeaveBattle no longer finalizes.
    private static void ApplyLeave(PartyBase party)
    {
        using (new AllowedThread())
        {
            if (party.MapEventSide != null)
                party.MapEventSide = null;

            if (party == PartyBase.MainParty && PlayerEncounter.Current != null)
                PlayerEncounter.Finish(false);
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