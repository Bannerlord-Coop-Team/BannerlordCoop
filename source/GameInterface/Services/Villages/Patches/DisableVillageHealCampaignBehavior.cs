using HarmonyLib;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using GameInterface.Policies;

namespace GameInterface.Services.Villages.Patches;

[HarmonyPatch(typeof(VillageHealCampaignBehavior))]
internal class DisableVillageHealCampaignBehavior
{
    [HarmonyPatch(nameof(VillageHealCampaignBehavior.RegisterEvents))]
    static bool Prefix()
    {
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;
        return false;
    }
}
