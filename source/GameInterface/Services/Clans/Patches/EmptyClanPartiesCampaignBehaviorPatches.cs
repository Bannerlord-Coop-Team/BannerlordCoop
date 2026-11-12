using Common;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.Library;

namespace GameInterface.Services.Clans.Patches;

[HarmonyPatch(typeof(EmptyClanPartiesCampaignBehavior))]
internal class EmptyClanPartiesCampaignBehaviorPatches
{
    [HarmonyPatch(nameof(EmptyClanPartiesCampaignBehavior.RegisterEvents))]
    [HarmonyPrefix]
    private static bool RegisterEventsPrefix() => false;

    [HarmonyPatch(nameof(EmptyClanPartiesCampaignBehavior.GetEmptyClanPartyLeaders))]
    [HarmonyPrefix]
    private static bool GetEmptyClanPartyLeadersPrefix(ref MBReadOnlyList<Hero> __result)
    {
        if (ModInformation.IsServer) return true;

        __result = new MBList<Hero>();
        return false;
    }
}
