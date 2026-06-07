using HarmonyLib;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using GameInterface.Policies;

namespace GameInterface.Services.Heroes.Patches.Disable;


[HarmonyPatch(typeof(HeroAgentSpawnCampaignBehavior))]
internal class DisableHeroAgentSpawnCampaignBehavior
{
    [HarmonyPatch(nameof(HeroAgentSpawnCampaignBehavior.RegisterEvents))]
    static bool Prefix()
    {
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;
        return false;
    }
}
