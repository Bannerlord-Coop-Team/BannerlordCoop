using HarmonyLib;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using GameInterface.Policies;

namespace GameInterface.Services.Heroes.Patches.Disable;


[HarmonyPatch(typeof(HeroKnownInformationCampaignBehavior))]
internal class DisableHeroKnownInformationCampaignBehavior
{
    [HarmonyPatch(nameof(HeroKnownInformationCampaignBehavior.RegisterEvents))]
    static bool Prefix()
    {
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;
        return false;
    }
}
