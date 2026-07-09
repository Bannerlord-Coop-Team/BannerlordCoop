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
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

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
        messageBroker.Subscribe<MapEventFinalized>(Handle_MapEventFinalized);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<NetworkBattleStartRequest>(Handle_NetworkBattleStartRequest);
        messageBroker.Unsubscribe<NetworkStartAttackMission>(Handle_NetworkStartAttackMission);
        messageBroker.Unsubscribe<MapEventFinalized>(Handle_MapEventFinalized);
    }

    /// <summary>The battle ended — drop its cached terrain seed (server-side; a no-op on a client's empty map).</summary>
    private void Handle_MapEventFinalized(MessagePayload<MapEventFinalized> payload)
    {
        if (objectManager.TryGetId(payload.What.MapEvent, out var mapEventId))
            mapEventTerrainSeeds.TryRemove(mapEventId, out _);
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

                operation = "read campaign atmosphere";
                AtmosphereInfo atmosphereOnCampaign = GetAtmosphereOnCampaign(mapEvent);

                operation = "send attack mission start";
                network.SendAll(new NetworkStartAttackMission(payload.What.MapEventId, randomTerrainSeed, atmosphereOnCampaign));

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

    private void OpenAttackMission(string mapEventId, int randomTerrainSeed, AtmosphereInfo atmosphereOnCampaign)
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

            if (!ContainerProvider.TryResolve(out IObjectManager objectManager) ||
                !objectManager.TryGetId(battle, out var battleMapEventId) ||
                battleMapEventId != mapEventId)
            {
                Logger.Warning("Received {Message} for map event {MapEventId}, but the local player is not in that battle; not opening battle mission", nameof(NetworkStartAttackMission), mapEventId);
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

            MissionInitializerRecord rec2 = missionInitializerResolver.Create(battle, randomTerrainSeed, atmosphereOnCampaign);

            // Engage the spawn gate BEFORE OpenBattleMission builds the mission — the deployment controller
            // spawns the initial wave during mission setup (inside OpenBattleMission), earlier than the
            // CoopBattleController attach. The gate only marks "a coop battle is active" for the spawn patches;
            // who fields which troops is decided by the server-fed reserves (CoopTroopSupplier).
            if (BattleSpawnConfig.Enabled)
            {
                BattleSpawnGate.BeginBattle(battleMapEventId);
                Logger.Information("[BattleSync] Engaged spawn gate in OpenAttackMission: mapEvent={MapEventId}", battleMapEventId);
            }

            // Coop opens a custom battle mission (per-client troop suppliers) instead of the native one; the
            // launcher lives in Missions and is resolved from the container. Fall back to the native mission only
            // if it is somehow unavailable, so a misconfiguration still yields a battle.
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
}
