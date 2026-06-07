using HarmonyLib;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using GameInterface.Policies;

namespace GameInterface.Services.Villages.Patches;

[HarmonyPatch(typeof(VillagerCampaignBehavior))]
internal class DisableVillagerCampaignBehavior
{
    [HarmonyPatch(nameof(VillagerCampaignBehavior.RegisterEvents))]
    static bool Prefix()
    {
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;
        return false;
    }
}
