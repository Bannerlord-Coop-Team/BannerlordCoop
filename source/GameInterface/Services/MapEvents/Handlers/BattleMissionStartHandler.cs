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
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.Map;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace GameInterface.Services.MapEvents.Handlers;

/// <summary>
/// Owns the live battle-mission start flow (split out of <see cref="BattleHandler"/>). On the server it answers the
/// mission-mode <see cref="NetworkBattleStartRequest"/>: gate it against <see cref="ServerBattleModeArbiter"/>, apply
/// the attack's hostile consequences, make the sides mission-ready, reply, tell the requester to open the mission
/// (<see cref="NetworkStartAttackMission"/>), and claim the mission mode on every client
/// (<see cref="NetworkBattleModeSet"/>). On the requesting client it opens the coop field-battle mission.
/// </summary>
internal class BattleMissionStartHandler : IHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger<BattleMissionStartHandler>();

    // Exclusive upper bound for the terrain seed, preserving the range of the original
    // client-side MBRandom.RandomInt(10000) roll this replaces.
    private const int MaxTerrainSeed = 10000;

    // The -10 player-hostility relation penalty vanilla applies against the target faction leader
    // when an attack first turns a faction hostile (the war block of BeHostileAction).
    private const int PlayerHostilityRelationPenalty = -10;

    private readonly IMessageBroker messageBroker;
    private readonly IObjectManager objectManager;
    private readonly INetwork network;
    private readonly IMapEventLogger mapEventLogger;

    // Server-side: terrain seed chosen once per map event and reused for every client that opens the same battle,
    // so they all use the same terrain seed. Keyed by map event id.
    private readonly ConcurrentDictionary<string, int> mapEventTerrainSeeds = new ConcurrentDictionary<string, int>();
    private readonly Random terrainSeedRandom = new Random();

    public BattleMissionStartHandler(
        IMessageBroker messageBroker,
        IObjectManager objectManager,
        INetwork network,
        IMapEventLogger mapEventLogger)
    {
        this.messageBroker = messageBroker;
        this.objectManager = objectManager;
        this.network = network;
        this.mapEventLogger = mapEventLogger;

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
    /// mission-ready, send the requester the mission start, and reply. Requests for other modes are ignored here.</summary>
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
            // CoopBattleController attach. The gate only marks "a coop battle is active" for the spawn patches;
            // who fields which troops is decided by the server-fed reserves (CoopTroopSupplier).
            if (BattleSpawnConfig.Enabled
                && ContainerProvider.TryResolve(out IObjectManager battleObjectManager)
                && battleObjectManager.TryGetId(battle, out var battleMapEventId))
            {
                BattleSpawnGate.BeginBattle(battleMapEventId);
                Logger.Information("[BattleSync] Engaged spawn gate in OpenAttackMission: mapEvent={MapEventId}", battleMapEventId);
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
}
