using Common;
using HarmonyLib;
using NavalDLC.CampaignBehaviors;

namespace Coop.Naval.Patches;

[HarmonyPatch(typeof(NavalDLCFigureheadCampaignBehavior))]
internal class NavalDLCFigureheadCampaignBehaviorPatches
{
    // The server has no player to notify, and each player's last loot time is kept in NavalPlayerData
    [HarmonyPatch(nameof(NavalDLCFigureheadCampaignBehavior.OnFigureheadUnlocked))]
    [HarmonyPrefix]
    private static bool OnFigureheadUnlockedPrefix() => ModInformation.IsClient;
}
