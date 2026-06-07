using HarmonyLib;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using GameInterface.Policies;

namespace GameInterface.Services.Kingdoms.Patches;

[HarmonyPatch(typeof(InfluenceGainCampaignBehavior))]
internal class DisableInfluenceGainCampaignBehavior
{
    [HarmonyPatch(nameof(InfluenceGainCampaignBehavior.RegisterEvents))]
    static bool Prefix()
    {
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;
        return false;
    }
}
