using Common;
using Common.Logging;
using Common.Messaging;
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
    // The host is computed locally (deterministic, no server round-trip) so the gate already knows whether to
    // suppress this client's spawn when the troops spawn. The server's authoritative assignment still arrives
    // later and reconciles the gate (BattleHostHandler.SetLocalHost).
    // SOAK-LOG PHASE of retiring this native battle-entry pipeline (RANK 12 — planned demotion).
    // Both coop launchers open battles via MissionState.OpenNew, which never routes through the patched
    // CampaignMission.OpenBattleMission wrapper, so in coop play this native path provably never fires. A future
    // release will demote this whole patch to a blocking guard (log an error + return false) under the SAME
    // predicate this soak warning uses — a coop session is active (a coop client campaign; the in-process server
    // seat keeps its behavior via the IsServer early-return below), NOT merely BattleSpawnGate.IsCoopBattleActive
    // (on the blocked native path the gate is not yet engaged). The warning fires under that exact predicate so
    // the soak validates the real block condition before the demotion lands. If it ever shows up in the logs,
    // some vanilla flow (a quest, a re-enabled hideout) still routes through the native open and must be handled
    // by the unified launcher pipeline before the guard is allowed to block it.
    [HarmonyPrefix]
    private static void Prefix(MissionInitializerRecord __0)
    {
        if (ModInformation.IsServer) return;

        // A live coop DI container == a coop session is running (client or server). This is the predicate the
        // final guard will block on; log loudly here first (no behavior change) to prove it never fires in coop.
        if (ContainerProvider.TryGetContainer(out _))
        {
            Logger.Warning(
                "[BattleSync][SOAK] Native CampaignMission.OpenBattleMission executed while a coop session is active — scene '{Scene}'. " +
                "This native battle-entry path is slated for demotion to a blocking guard; the coop launchers (MissionState.OpenNew) are meant to be the only battle-entry layer.",
                __0.SceneName);
        }

        if (!BattleSpawnConfig.Enabled) return;

        var mapEvent = PlayerEncounter.Battle ?? MobileParty.MainParty?.MapEvent;
        if (mapEvent == null) return;

        if (!ContainerProvider.TryResolve(out IObjectManager objectManager)) return;
        if (!objectManager.TryGetId(mapEvent, out var mapEventId)) return;

        BattleSpawnGate.BeginBattle(mapEventId);
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
