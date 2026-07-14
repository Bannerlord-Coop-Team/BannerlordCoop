using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using Common.Util;
using GameInterface.Services.MapEvents.Logging;
using GameInterface.Services.MapEvents.Messages.Leave;
using GameInterface.Services.MapEvents.Messages.Start;
using GameInterface.Services.ObjectManager;
using LiteNetLib;
using Serilog;
using System;
using System.Collections.Concurrent;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Encounters;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem.Map;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TaleWorlds.ObjectSystem;

namespace GameInterface.Services.MapEvents.Handlers;

/// <summary>
/// Owns the live battle-mission start flow (split out of <see cref="BattleHandler"/>). On the server it answers the
/// mission-mode <see cref="NetworkBattleStartRequest"/>: gate it against <see cref="ServerBattleModeArbiter"/>, apply
/// the attack's hostile consequences, make the sides mission-ready, reply, broadcast the mission start
/// (<see cref="NetworkStartAttackMission"/>), and claim the mission mode on every client
/// (<see cref="NetworkBattleModeSet"/>). Clients in the map event open the coop field-battle mission.
/// </summary>
internal class BattleMissionStartHandler : IHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger<BattleMissionStartHandler>();

    // Exclusive upper bound for the terrain seed, preserving the range of the original
    // client-side MBRandom.RandomInt(10000) roll this replaces.
    private const int MaxTerrainSeed = 10000;

    private readonly IMessageBroker messageBroker;
    private readonly IObjectManager objectManager;
    private readonly INetwork network;
    private readonly IMapEventLogger mapEventLogger;
    private readonly IBattleMissionInitializerResolver missionInitializerResolver;

    // Server-side: terrain seed chosen once per map event and reused for every client that opens the same battle,
    // so they all use the same terrain seed. Keyed by map event id.
    private readonly ConcurrentDictionary<string, int> mapEventTerrainSeeds = new ConcurrentDictionary<string, int>();
    private readonly Random terrainSeedRandom = new Random();

    // Server-side: the siege mission inputs (wall level, wall HPs, engine lists) snapshotted once per map event,
    // so a joiner entering mid-assault loads the same scene as the first entrant even though the campaign-side
    // container keeps syncing. Evicted with the terrain seed when the event finalizes.
    private readonly ConcurrentDictionary<string, NetworkStartSiegeMission> siegeMissionSnapshots = new ConcurrentDictionary<string, NetworkStartSiegeMission>();

    public BattleMissionStartHandler(
        IMessageBroker messageBroker,
        IObjectManager objectManager,
        INetwork network,
        IMapEventLogger mapEventLogger,
        IBattleMissionInitializerResolver missionInitializerResolver)
    {
        this.messageBroker = messageBroker;
        this.objectManager = objectManager;
        this.network = network;
        this.mapEventLogger = mapEventLogger;
        this.missionInitializerResolver = missionInitializerResolver;

        messageBroker.Subscribe<NetworkBattleStartRequest>(Handle_NetworkBattleStartRequest);
        messageBroker.Subscribe<NetworkStartAttackMission>(Handle_NetworkStartAttackMission);
        messageBroker.Subscribe<NetworkStartSiegeMission>(Handle_NetworkStartSiegeMission);
        messageBroker.Subscribe<MapEventFinalized>(Handle_MapEventFinalized);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<NetworkBattleStartRequest>(Handle_NetworkBattleStartRequest);
        messageBroker.Unsubscribe<NetworkStartAttackMission>(Handle_NetworkStartAttackMission);
        messageBroker.Unsubscribe<NetworkStartSiegeMission>(Handle_NetworkStartSiegeMission);
        messageBroker.Unsubscribe<MapEventFinalized>(Handle_MapEventFinalized);
    }

    /// <summary>The battle ended — drop its cached mission inputs (server-side; a no-op on a client's empty maps).</summary>
    private void Handle_MapEventFinalized(MessagePayload<MapEventFinalized> payload)
    {
        if (objectManager.TryGetId(payload.What.MapEvent, out var mapEventId))
        {
            mapEventTerrainSeeds.TryRemove(mapEventId, out _);
            siegeMissionSnapshots.TryRemove(mapEventId, out _);
        }
    }

    /// <summary>[Server] Handle a battle-start request for the live-mission mode: gate it, make the sides
    /// mission-ready, broadcast the mission start, and reply. Requests for other modes are ignored here.</summary>
    private void Handle_NetworkBattleStartRequest(MessagePayload<NetworkBattleStartRequest> payload)
    {
        if (ModInformation.IsClient)
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
        GameThread.RunSafe(() =>
        {
            var operation = "resolve map event";

            try
            {
                if (!objectManager.TryGetObject(payload.What.MapEventId, out MapEvent mapEvent))
                    return;

                operation = "validate hostile action mode";
                if (mapEvent.IsUnsupportedMultiPlayerHostileAction())
                {
                    Logger.Warning("Rejecting attack mission start for map event {MapEventId}: this hostile action does not support multiple player parties", payload.What.MapEventId);
                    network.Send(requester, new NetworkBattleStartReply(payload.What.RequestId, false));
                    return;
                }

                // The lords-hall stage is not supported: CurrentSiegeState never advances past OnTheWalls in
                // co-op (SiegeMissionEndPatches), so this only trips on a save that carried the state in.
                // Rejected before the arbiter claim so the event stays open for auto-resolve.
                operation = "validate siege stage";
                if (mapEvent.IsSiegeAssault && mapEvent.MapEventSettlement?.CurrentSiegeState == Settlement.SiegeState.InTheLordsHall)
                {
                    Logger.Error("Rejecting siege mission for {MapEventId}: lords-hall stage is not supported", payload.What.MapEventId);
                    network.Send(requester, new NetworkBattleStartReply(payload.What.RequestId, false));
                    return;
                }

                // Server-authoritative mode gate: accept the live mission only if no auto-resolve simulation already
                // owns this event. On reject, don't make the sides mission-ready or reply — the requesting client
                // waits for NetworkStartAttackMission to open the mission, so it simply stays at the encounter menu.
                operation = "claim mission mode";
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
                operation = "apply attack hostile-action consequences";
                ApplyClientAttackHostileConsequences(mapEvent, payload.What.AttackerPartyId);

                operation = "make map event sides mission-ready";
                foreach (var side in mapEvent._sides)
                {
                    side.MakeReadyForMission(null);
                }

                // Reply first so the requesting client's blocked consequence unblocks before the mission-open
                // message arrives — the mission then opens off the menu-consequence stack, as in the pre-coordinator
                // flow, rather than re-entrantly during the blocking wait.
                operation = "send battle start reply";
                network.Send(requester, new NetworkBattleStartReply(payload.What.RequestId, true));

                if (mapEvent.IsSiegeAssault)
                {
                    operation = "send siege mission snapshot";
                    var snapshot = siegeMissionSnapshots.GetOrAdd(payload.What.MapEventId, _ => BuildSiegeMissionSnapshot(payload.What.MapEventId, mapEvent));
                    // Broadcast like the field path: every involved player (co-besiegers, a player defender) opens the
                    // mission and joins the leader-hosted assault. Non-involved clients self-filter in OpenSiegeMission
                    // (TryGetValidBattle needs their own PlayerEncounter.Battle), so only participants open it.
                    network.SendAll(snapshot);
                }
                else
                {
                    operation = "read campaign atmosphere";
                    AtmosphereInfo atmosphereOnCampaign = GetAtmosphereOnCampaign(mapEvent);

                    operation = "send attack mission start";
                    network.SendAll(new NetworkStartAttackMission(payload.What.MapEventId, randomTerrainSeed, atmosphereOnCampaign));
                }

                // Claim the event for the mission mode on every client, so one still sitting at the encounter menu
                // greys out the auto-resolve option — a map event is fought as a live mission XOR an auto-resolve,
                // never both (see BattleModeEncounterOptionsPatch / BattleModeRegistry).
                operation = "send battle mode";
                network.SendAll(new NetworkBattleModeSet(payload.What.MapEventId, (int)BattleStartMode.Mission));
            }
            catch (Exception e)
            {
                Logger.Error(e, "Failed to {Operation} for {Message}", operation, nameof(NetworkBattleStartRequest));
                network.Send(requester, new NetworkBattleStartReply(payload.What.RequestId, false));
            }
        }, context: nameof(Handle_NetworkBattleStartRequest));
    }

    private static AtmosphereInfo GetAtmosphereOnCampaign(MapEvent mapEvent)
    {
        var weatherModel = Campaign.Current?.Models?.MapWeatherModel;
        if (weatherModel == null)
            return default;

        try
        {
            return weatherModel.GetAtmosphereModel(mapEvent.Position);
        }
        catch (Exception e)
        {
            Logger.Warning(e, "Failed to read campaign atmosphere for map event; using default atmosphere");
            return default;
        }
    }

    /// <summary>
    /// When a client attacks a not-already-hostile party, declares war on the target faction and
    /// applies the player-hostility relation penalty against its leader - the war block of vanilla
    /// MenuHelper.EncounterAttackConsequence (BeHostileAction.ApplyEncounterHostileAction), which
    /// neither the client (it defers to the server) nor the dedicated server (it never opens the
    /// encounter menu) runs.
    /// </summary>
    private void ApplyClientAttackHostileConsequences(MapEvent mapEvent, string attackerPartyId)
    {
        if (!objectManager.TryGetObject(attackerPartyId, out MobileParty attackerMobileParty))
        {
            Logger.Warning("Could not resolve attacker party {AttackerPartyId} for attack hostile-action consequences", attackerPartyId);
            return;
        }

        MapEventHostileActionConsequences.Apply(mapEvent, attackerMobileParty.Party, "attack");
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
        var message = payload.What;
        GameThread.RunSafe(
            () => OpenAttackMission(message.MapEventId, message.RandomTerrainSeed, message.AtmosphereOnCampaign),
            context: nameof(Handle_NetworkStartAttackMission));
    }

    /// <summary>[Server] Snapshot the mission-defining siege inputs for one map event.</summary>
    private static NetworkStartSiegeMission BuildSiegeMissionSnapshot(string mapEventId, MapEvent mapEvent)
    {
        var settlement = mapEvent.MapEventSettlement;
        var siegeEvent = settlement.SiegeEvent;
        int wallLevel = settlement.Town.GetWallLevel();

        var attackerEngines = SiegeEngineStateConverter.ToEngineStates(siegeEvent.GetPreparedAndActiveSiegeEngines(siegeEvent.GetSiegeEventSide(BattleSideEnum.Attacker)));
        var defenderEngines = SiegeEngineStateConverter.ToEngineStates(siegeEvent.GetPreparedAndActiveSiegeEngines(siegeEvent.GetSiegeEventSide(BattleSideEnum.Defender)));

        return new NetworkStartSiegeMission(mapEventId, wallLevel,
            settlement.SettlementWallSectionHitPointsRatioList.ToArray(), attackerEngines, defenderEngines);
    }

    private void Handle_NetworkStartSiegeMission(MessagePayload<NetworkStartSiegeMission> payload)
    {
        var message = payload.What;
        GameThread.Run(() => OpenSiegeMission(message));
    }

    private void OpenSiegeMission(NetworkStartSiegeMission payload)
    {
        bool spawnGateEngaged = false;
        try
        {
            if (!TryGetValidBattle(nameof(NetworkStartSiegeMission), payload.MapEventId, out var battle))
                return;

            var settlement = battle.MapEventSettlement;
            if (settlement == null)
            {
                Logger.Warning("Received {Message} but the battle has no settlement, not opening siege mission", nameof(NetworkStartSiegeMission));
                return;
            }

            // The scene is the fixed settlement scene keyed by wall level — no terrain seed on the siege
            // path. Mirrors vanilla CreateSandBoxMissionInitializerRecord; atmosphere is client-local,
            // same tolerance as the field path.
            string sceneName = settlement.LocationComplex.GetLocationWithId("center").GetSceneName(payload.WallLevel);
            var rec = new MissionInitializerRecord(sceneName)
            {
                SceneLevels = Campaign.Current.Models.LocationModel.GetUpgradeLevelTag(payload.WallLevel) + " siege",
                DamageToFriendsMultiplier = Campaign.Current.Models.DifficultyModel.GetPlayerTroopsReceivedDamageMultiplier(),
                DamageFromPlayerToFriendsMultiplier = Campaign.Current.Models.DifficultyModel.GetPlayerTroopsReceivedDamageMultiplier(),
                PlayingInCampaignMode = true,
                AtmosphereOnCampaign = Campaign.Current.Models.MapWeatherModel.GetAtmosphereModel(MobileParty.MainParty.Position),
                TerrainType = (int)Campaign.Current.MapSceneWrapper.GetFaceTerrainType(MobileParty.MainParty.CurrentNavigationFace),
                DecalAtlasGroup = 3,
            };

            var attackerWeapons = SiegeEngineStateConverter.ToMissionWeapons(payload.AttackerEngines);
            var defenderWeapons = SiegeEngineStateConverter.ToMissionWeapons(payload.DefenderEngines);

            if (BattleSpawnConfig.Enabled)
            {
                BattleSpawnGate.BeginBattle(payload.MapEventId);
                spawnGateEngaged = true;
                Logger.Information("[BattleSync] Engaged spawn gate in OpenSiegeMission: mapEvent={MapEventId}", payload.MapEventId);
            }

            // No native fallback: SandBoxMissions.OpenSiegeMissionWithDeployment would size to whole-side
            // counts and never attach the coop behaviors, so a missing launcher is a hard error.
            if (ContainerProvider.TryResolve(out ICoopSiegeBattleLauncher siegeLauncher))
            {
                var mission = siegeLauncher.OpenCoopSiegeBattle(rec, payload.WallHitPointRatios, attackerWeapons, defenderWeapons);
                if (mission != null)
                    spawnGateEngaged = false; // the attached mission lifecycle owns EndBattle from here
                else
                    Logger.Error("[BattleSync] Coop siege launcher returned no mission");
            }
            else
            {
                Logger.Error("[BattleSync] ICoopSiegeBattleLauncher unavailable; cannot open the siege mission");
            }
        }
        catch (Exception e)
        {
            // GameThread runs queued actions unguarded, so a throw from here
            // would escape into the game's main tick and crash it.
            Logger.Error(e, "Failed to open the siege mission for {Message}", nameof(NetworkStartSiegeMission));
        }
        finally
        {
            UnwindSpawnGateAfterFailedOpen(spawnGateEngaged);
        }
    }

    /// <summary>[Client] Re-validates everything a mission open depends on: the encounter can end (or
    /// another mission can open) between the server round-trip and the queued open running, and a
    /// finalized battle keeps PlayerEncounter.Battle set while releasing the main party. The
    /// MissionState check covers a second start queued in the same frame (it is set synchronously by
    /// the state push, unlike Mission.Current).</summary>
    private bool TryGetValidBattle(string messageName, string expectedMapEventId, out MapEvent battle)
    {
        battle = null;
        if (Campaign.Current == null)
        {
            Logger.Warning("Received {Message} but the campaign was not loaded, not opening the mission", messageName);
            return false;
        }

        battle = PlayerEncounter.Battle;
        if (battle == null)
        {
            Logger.Warning("Received {Message} but PlayerEncounter.Battle was null, not opening the mission", messageName);
            return false;
        }

        if (!MatchesMapEventId(objectManager, battle, expectedMapEventId))
        {
            Logger.Warning("Received {Message} for map event {MapEventId}, but the local player is not in that battle; not opening the mission", messageName, expectedMapEventId);
            return false;
        }

        if (MobileParty.MainParty?.MapEvent == null)
        {
            Logger.Warning("Received {Message} but the main party is no longer in a map event, not opening the mission", messageName);
            return false;
        }

        if (MissionState.Current != null)
        {
            Logger.Warning("Received {Message} but a mission is already open, not opening the mission", messageName);
            return false;
        }

        return true;
    }

    /// <summary>Pure routing check shared by the queued siege-open path and its regression tests.</summary>
    internal static bool MatchesMapEventId(IObjectManager objectManager, MapEvent battle, string expectedMapEventId)
    {
        return objectManager != null
            && battle != null
            && objectManager.TryGetId(battle, out var actualMapEventId)
            && string.Equals(actualMapEventId, expectedMapEventId, StringComparison.Ordinal);
    }

    private void OpenAttackMission(string mapEventId, int randomTerrainSeed, AtmosphereInfo atmosphereOnCampaign)
    {
        bool spawnGateEngaged = false;
        try
        {
            // The encounter can end (or another mission can open) between the server
            // round-trip and this running, so everything the mission depends on is
            // re-validated here rather than at message arrival.
            if (!TryGetValidBattle(nameof(NetworkStartAttackMission), mapEventId, out var battle))
                return;

            MissionInitializerRecord rec2 = missionInitializerResolver.Create(battle, randomTerrainSeed, atmosphereOnCampaign);

            // Engage the spawn gate BEFORE OpenBattleMission builds the mission — the deployment controller
            // spawns the initial wave during mission setup (inside OpenBattleMission), earlier than the
            // CoopBattleController attach. The gate only marks "a coop battle is active" for the spawn patches;
            // who fields which troops is decided by the server-fed reserves (CoopTroopSupplier).
            if (BattleSpawnConfig.Enabled)
            {
                BattleSpawnGate.BeginBattle(mapEventId);
                spawnGateEngaged = true;
                Logger.Information("[BattleSync] Engaged spawn gate in OpenAttackMission: mapEvent={MapEventId}", mapEventId);
            }

            // Coop opens a custom battle mission (per-client troop suppliers) instead of the native one; the
            // launcher lives in Missions and is resolved from the container. There is deliberately no native
            // fallback: the same unavailable container would prevent BattleMissionEntryPatch from attaching the
            // lifecycle that owns EndBattle, while the already-engaged spawn patches could corrupt native setup.
            if (ContainerProvider.TryResolve(out ICoopFieldBattleLauncher battleLauncher))
            {
                var mission = battleLauncher.OpenCoopFieldBattle(rec2);
                if (mission != null)
                    spawnGateEngaged = false; // the attached mission lifecycle owns EndBattle from here
                else
                    Logger.Error("[BattleSync] Coop field-battle launcher returned no mission");
            }
            else
            {
                Logger.Error("[BattleSync] ICoopFieldBattleLauncher unavailable; cannot safely open the field battle mission");
            }
        }
        catch (Exception e)
        {
            // GameThread runs queued actions unguarded, so a throw from here
            // would escape into the game's main tick and crash it.
            Logger.Error(e, "Failed to open the battle mission for {Message}", nameof(NetworkStartAttackMission));
        }
        finally
        {
            UnwindSpawnGateAfterFailedOpen(spawnGateEngaged);
        }
    }

    internal static void UnwindSpawnGateAfterFailedOpen(bool spawnGateEngaged)
    {
        if (spawnGateEngaged) BattleSpawnGate.EndBattle();
    }
}
