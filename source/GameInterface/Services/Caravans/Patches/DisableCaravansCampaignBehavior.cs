using HarmonyLib;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using GameInterface.Policies;

namespace GameInterface.Services.Caravans.Patches;

[HarmonyPatch(typeof(CaravansCampaignBehavior))]
internal class DisableCaravansCampaignBehavior
{
    [HarmonyPatch(nameof(CaravansCampaignBehavior.RegisterEvents))]
    static bool Prefix()
    {
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;
        return false;
    }
}
