using HarmonyLib;
using TaleWorlds.CampaignSystem.Issues;
using GameInterface.Policies;

namespace GameInterface.Services.Villages.Patches;

[HarmonyPatch(typeof(VillageNeedsToolsIssueBehavior))]
internal class DisableVillageNeedsToolsIssueBehavior
{
    [HarmonyPatch(nameof(VillageNeedsToolsIssueBehavior.RegisterEvents))]
    static bool Prefix()
    {
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;
        return false;
    }
}
