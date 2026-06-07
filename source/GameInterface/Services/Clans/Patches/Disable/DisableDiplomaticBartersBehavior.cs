using HarmonyLib;
using TaleWorlds.CampaignSystem.CampaignBehaviors.BarterBehaviors;
using GameInterface.Policies;

namespace GameInterface.Services.Clans.Patches.Disable;

[HarmonyPatch(typeof(DiplomaticBartersBehavior))]
internal class DisableDiplomaticBartersBehavior
{
    [HarmonyPatch(nameof(DiplomaticBartersBehavior.RegisterEvents))]
    static bool Prefix()
    {
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;
        return false;
    }
}
