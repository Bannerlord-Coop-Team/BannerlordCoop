using Common;
using Common.Logging;
using HarmonyLib;
using Serilog;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;

namespace GameInterface.Services.MapEvents.Patches;

[HarmonyPatch]
internal class DebugMapEventPatches
{
    private static readonly ILogger Logger = LogManager.GetLogger<DebugMapEventPatches>();

    [HarmonyPatch(typeof(TroopUpgradeTracker), nameof(TroopUpgradeTracker.CalculateReadyToUpgradeSafe))]
    [HarmonyPrefix]
    private static bool PrefixBattleState(TroopUpgradeTracker __instance, ref TroopRosterElement el, PartyBase owner, int __result)
    {
        // Disable on client for now
        if (ModInformation.IsClient)
        {
            __result = 0;
            return false;
        }

        return true;
    }
}