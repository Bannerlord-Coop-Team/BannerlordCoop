using Common;
using GameInterface.Policies;
using HarmonyLib;
using SandBox.Missions.MissionLogics;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Siege;
using TaleWorlds.Core;

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
