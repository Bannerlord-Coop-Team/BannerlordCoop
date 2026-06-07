using HarmonyLib;
using TaleWorlds.CampaignSystem.Issues;
using GameInterface.Policies;

namespace GameInterface.Services.Villages.Patches;

[HarmonyPatch(typeof(VillageNeedsCraftingMaterialsIssueBehavior))]
internal class DisableVillageNeedsCraftingMaterialsIssueBehavior
{
    [HarmonyPatch(nameof(VillageNeedsCraftingMaterialsIssueBehavior.RegisterEvents))]
    static bool Prefix()
    {
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;
        return false;
    }
}

