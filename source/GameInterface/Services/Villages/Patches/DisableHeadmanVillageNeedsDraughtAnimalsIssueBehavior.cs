using HarmonyLib;
using TaleWorlds.CampaignSystem.Issues;
using GameInterface.Policies;

namespace GameInterface.Services.Villages.Patches;

[HarmonyPatch(typeof(HeadmanVillageNeedsDraughtAnimalsIssueBehavior))]
internal class DisableHeadmanVillageNeedsDraughtAnimalsIssueBehavior
{
    [HarmonyPatch(nameof(HeadmanVillageNeedsDraughtAnimalsIssueBehavior.RegisterEvents))]
    static bool Prefix()
    {
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;
        return false;
    }
}
