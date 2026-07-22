using Common;
using Common.Logging;
using Common.Messaging;
using GameInterface.Services.MapEvents.BattleSize;
using GameInterface.Services.MapEvents.Messages;
using GameInterface.Services.ObjectManager;
using HarmonyLib;
using Serilog;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace GameInterface.Services.MapEvents.Patches;

/// <summary>
/// Attaches the P2P battle behaviors and raises <see cref="PlayerEnteredBattle"/> when the local player
/// opens a field-battle mission, kicking off the mission-scoped P2P instance request (instance id = the
/// map event's object-manager id, so every player in the same battle joins the same instance). The battle
/// counterpart to <see cref="Locations.Patches.PlayerLocationEntryPatches"/>; the headless server never
/// opens a battle mission for itself, so this is a no-op there.
/// </summary>
// CampaignMission.OpenBattleMission has two overloads — pin the MissionInitializerRecord one (the field-
// battle path BattleHandler.OpenAttackMission uses). Without the explicit signature AccessTools.Method is
// ambiguous and PatchAll throws, which aborts ALL GameInterface patching.
[HarmonyPatch(typeof(CampaignMission), nameof(CampaignMission.OpenBattleMission), new[] { typeof(MissionInitializerRecord) })]
internal class BattleMissionEntryPatch
{
    private static readonly ILogger Logger = LogManager.GetLogger<BattleMissionEntryPatch>();

    // OpenBattleMission can be reached more than once around a single encounter; attach the P2P behaviors
    // exactly once per mission. ConditionalWeakTable lets the mission be GC'd freely.
    private static readonly ConditionalWeakTable<Mission, object> AttachedMissions = new();

    // Engage the spawn gate BEFORE OpenBattleMission builds the mission, because the deployment controller
    // spawns the initial wave during mission setup (inside OpenBattleMission) — earlier than the postfix.
    // The gate holds no host/ownership state: which client fields a party is decided server-side by the
    // troop reserve assignment (BattleTroopReserveBuilder), and the host is elected when the first client
    // reports mission-ready (BattleHostHandler). BeginBattle only marks the coop battle active for the
    // spawn/battle patches.
    [HarmonyPrefix]
    private static void Prefix()
    {
        if (ModInformation.IsServer) return;
        if (!BattleSpawnConfig.Enabled) return;
        if (BattleSpawnGate.IsCoopBattleActive) return;

        var mapEvent = PlayerEncounter.Battle ?? MobileParty.MainParty?.MapEvent;
        if (mapEvent == null) return;

        if (!ContainerProvider.TryResolve(out IObjectManager objectManager)) return;
        if (!objectManager.TryGetId(mapEvent, out var mapEventId)) return;
        if (!ContainerProvider.TryResolve(out IServerBattleSizeProvider battleSizeProvider)) return;

        BattleSpawnGate.BeginBattle(mapEventId, battleSizeProvider.BattleSize);
        Logger.Information("[BattleSync] Engaged spawn gate before mission load: mapEvent={MapEventId}", mapEventId);
    }

    [HarmonyPostfix]
    private static void Postfix()
    {
        if (ModInformation.IsServer)
            return;

        // MissionState.Current is set synchronously by the state push that OpenBattleMission performs;
        // CurrentMission is the freshly opened mission (Mission.Current itself is only set on the first tick).
        var mission = MissionState.Current?.CurrentMission;
        if (mission == null)
        {
            Logger.Warning("[BattleSync] OpenBattleMission postfix ran with no current mission — cannot attach P2P behaviors");
            return;
        }

        // The map event the mission was opened for. PlayerEncounter.Battle is the authoritative source;
        // fall back to the main party's map event if the encounter has already moved on.
        var mapEvent = PlayerEncounter.Battle ?? MobileParty.MainParty?.MapEvent;
        if (mapEvent == null)
        {
            Logger.Warning("[BattleSync] OpenBattleMission for '{Scene}' with no resolvable map event — skipping instance request", mission.SceneName);
            return;
        }

        // Attach FIRST so the controller is alive and subscribed before PlayerEnteredBattle is published —
        // it owns the instance request and the join-info exchange (mirrors PlayerLocationEntryPatches).
        AttachBattleBehaviors(mission);

        MessageBroker.Instance.Publish(mapEvent, new PlayerEnteredBattle(mapEvent));
    }

    // GameInterface cannot reference the Missions P2P behaviors directly (Missions depends on GameInterface),
    // so the attacher — implemented in Missions, like ICoopFieldBattleLauncher — crosses the boundary.
    private static void AttachBattleBehaviors(Mission mission)
    {
        if (AttachedMissions.TryGetValue(mission, out _)) return;

        if (ContainerProvider.TryResolve(out ICoopBattleBehaviorAttacher attacher) == false)
        {
            Logger.Warning("[BattleSync] Mission container not available — cannot attach P2P battle behaviors to '{Scene}'", mission.SceneName);
            return;
        }

        AttachedMissions.Add(mission, null);
        attacher.Attach(mission);
    }
}
