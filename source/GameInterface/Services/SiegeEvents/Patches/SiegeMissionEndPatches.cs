using Common;
using GameInterface.Policies;
using GameInterface.Services.MapEvents;
using HarmonyLib;
using SandBox.Missions.MissionLogics;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Siege;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace GameInterface.Services.SiegeEvents.Patches;

/// <summary>
/// Gates the two campaign write-backs every machine's siege mission fires at local mission end.
/// </summary>
[HarmonyPatch]
internal class SiegeMissionEndPatches
{
    // The lords-hall stage is not supported in co-op: the walls mission runs until a side is depleted
    // and capture happens at the walls, exactly like vanilla's AI-led and auto-resolved assaults. This
    // state is the only route into the lords-hall mission, so it never advances on any machine.
    [HarmonyPatch(typeof(Settlement), nameof(Settlement.SetNextSiegeState))]
    [HarmonyPrefix]
    private static bool SetNextSiegeStatePrefix() => false;

    // A completed ambush always means the defenders returned to the castle, regardless of which side the local
    // player controls. SallyOutEndLogic derives success from PlayerTeam and would split the completion barrier.
    [HarmonyPatch(typeof(SallyOutEndLogic), nameof(SallyOutEndLogic.MissionEnded))]
    [HarmonyPostfix]
    private static void SallyOutMissionEndedPostfix(SallyOutEndLogic __instance,
        ref MissionResult missionResult, bool __result)
    {
        if (!__result || !BattleConclusionGate.IsInCoopBattleMission) return;
        if (PlayerEncounter.Battle?.IsSiegeAmbush != true || __instance.Mission?.PlayerTeam == null) return;

        missionResult = CreateSiegeAmbushResult(__instance.Mission.PlayerTeam.Side);
    }

    internal static MissionResult CreateSiegeAmbushResult(BattleSideEnum playerSide)
    {
        bool playerVictory = playerSide == BattleSideEnum.Defender;
        return new MissionResult(BattleState.DefenderVictory, playerVictory, !playerVictory, enemyRetreated: false);
    }

    // Defer the ambush map-event teardown to the coop result-ready barrier. Keep vanilla's local ambush-flag
    // cleanup, but do not let the elected host finalize before every current mission member reports.
    [HarmonyPatch(typeof(SiegeAmbushCampaignBehavior), "OnMissionEnded")]
    [HarmonyPrefix]
    private static bool SiegeAmbushOnMissionEndedPrefix()
    {
        if (ModInformation.IsClient && BattleConclusionGate.IsInCoopBattleMission
            && PlayerEncounter.Battle?.IsSiegeAmbush == true)
        {
            PlayerEncounter.Current?.SetIsSallyOutAmbush(false);
            return false;
        }

        return true;
    }

    // Every client's CampaignMissionComponent fires this when its local mission ends; the surviving
    // engine states are reported once by the mission host and applied here on the server with patches
    // live, so the HP writes and broken-engine removals replicate through the container sync.
    [HarmonyPatch(typeof(SiegeEvent), nameof(SiegeEvent.SetSiegeEngineStatesAfterSiegeMission))]
    [HarmonyPrefix]
    private static bool SetSiegeEngineStatesPrefix()
    {
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;

        return ModInformation.IsServer;
    }

    // The vanilla engine write-back call dereferences the attacker leader's SiegeEvent before the
    // gated call above can block it, and a server-side siege teardown (AI peace) replicating
    // mid-mission nulls that reference on clients. When it is gone, re-run the method minus the
    // engine block (sound teardown, mission-ended dispatch) instead of crashing the mission end.
    [HarmonyPatch(typeof(CampaignMissionComponent), nameof(CampaignMissionComponent.OnEndMission))]
    [HarmonyPrefix]
    private static bool CampaignMissionOnEndMissionPrefix(CampaignMissionComponent __instance)
    {
        if (Campaign.Current?.GameMode != CampaignGameMode.Campaign) return true;

        var battle = PlayerEncounter.Battle;
        if (battle == null || (!battle.IsSiegeAssault && !battle.IsSiegeAmbush)) return true;
        if (battle.GetLeaderParty(BattleSideEnum.Attacker)?.SiegeEvent != null) return true;

        if (__instance._soundEvent != null)
        {
            __instance.RemovePreviousAgentsSoundEvent();
            __instance._soundEvent.Stop();
            __instance._soundEvent = null;
        }

        CampaignEventDispatcher.Instance.OnMissionEnded(__instance.Mission);
        CampaignMission.Current = null;
        return false;
    }
}
