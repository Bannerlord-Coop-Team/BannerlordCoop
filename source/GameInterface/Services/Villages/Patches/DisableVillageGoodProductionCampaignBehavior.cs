using HarmonyLib;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using GameInterface.Policies;

namespace GameInterface.Services.Villages.Patches;

[HarmonyPatch(typeof(VillageGoodProductionCampaignBehavior))]
internal class DisableVillageGoodProductionCampaignBehavior
{
    [HarmonyPatch(nameof(VillageGoodProductionCampaignBehavior.RegisterEvents))]
    static bool Prefix()
    {
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;
        return false;
    }
}
