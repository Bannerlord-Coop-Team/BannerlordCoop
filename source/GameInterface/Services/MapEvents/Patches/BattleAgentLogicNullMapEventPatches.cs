using HarmonyLib;
using SandBox.Missions.MissionLogics;
using TaleWorlds.CampaignSystem.MapEvents;

namespace GameInterface.Services.MapEvents.Patches;

/// <summary>
/// Skips BattleAgentLogic's upgrade-reward path when the player's map event cannot serve it during a coop
/// battle. Its tracker getter is <c>MapEvent.PlayerMapEvent.TroopUpgradeTracker</c>, which NREs once the coop
/// client's map event degenerates mid-mission — throwing on EVERY hit (no damage lands, the rest of the game
/// tick dies) or every tick on the teardown loading screen. Two live shapes of "degenerate":
/// <list type="bullet">
/// <item>PlayerMapEvent is NULL — the client tore its map event down mid-mission (OnClientDestroyed nulls
/// MainParty's side while the mission is still live).</item>
/// <item>PlayerMapEvent points at a DIFFERENT, sync-created event with no initialized tracker — seen when the
/// server concluded the battle under a still-fighting successor (host retreat commit) and the captivity flow
/// re-attached the player's party mid-mission.</item>
/// </list>
/// The guard only fires in a coop battle; during a healthy fight the battle's own event has a live tracker and
/// vanilla runs normally.
/// </summary>
[HarmonyPatch]
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

    private static bool PlayerMapEventReady
    {
        get
        {
            if (!BattleSpawnGate.IsCoopBattleActive) return true; // vanilla battles untouched

            var playerMapEvent = MapEvent.PlayerMapEvent;
            return playerMapEvent != null && playerMapEvent.TroopUpgradeTracker != null;
        }
    }
}
