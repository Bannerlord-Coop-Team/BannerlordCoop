using HarmonyLib;
using SandBox.Missions.MissionLogics;
using TaleWorlds.CampaignSystem.MapEvents;

namespace GameInterface.Services.MapEvents.Patches;

/// <summary>
/// Skips BattleAgentLogic's upgrade-reward path when there is no player map event during a coop battle. Its tracker
/// getter is <c>MapEvent.PlayerMapEvent.TroopUpgradeTracker</c>, which NREs once the coop client tears its map event
/// down mid-mission (OnClientDestroyed nulls MainParty's side while the mission is still live), throwing every tick
/// and wedging the client on the loading screen. The guard only fires in that teardown tail (the battle has already finalized); during the fight PlayerMapEvent is non-null and vanilla runs normally.
/// </summary>
internal class BattleAgentLogicNullMapEventPatches
{
    [HarmonyPatch(typeof(BattleAgentLogic), nameof(BattleAgentLogic.OnScoreHit))]
    [HarmonyPrefix]
    private static bool Prefix_OnScoreHit() => PlayerMapEventReady;

    [HarmonyPatch(typeof(BattleAgentLogic), nameof(BattleAgentLogic.OnAgentBuild))]
    [HarmonyPrefix]
    private static bool Prefix_OnAgentBuild() => PlayerMapEventReady;

    // Guard CheckUpgrade rather than the whole OnAgentRemoved so its kill/wound/rout bookkeeping still runs.
    [HarmonyPatch(typeof(BattleAgentLogic), "CheckUpgrade")]
    [HarmonyPrefix]
    private static bool Prefix_CheckUpgrade() => PlayerMapEventReady;

    private static bool PlayerMapEventReady => !(MapEvent.PlayerMapEvent == null && BattleSpawnGate.IsCoopBattleActive);
}
