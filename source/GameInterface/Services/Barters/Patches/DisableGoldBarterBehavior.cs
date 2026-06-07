using HarmonyLib;
using TaleWorlds.CampaignSystem.CampaignBehaviors.BarterBehaviors;
using GameInterface.Policies;

namespace GameInterface.Services.Settlements.Patches;

[HarmonyPatch(typeof(GoldBarterBehavior))]
internal class DisableGoldBarterBehavior
{
    [HarmonyPatch(nameof(GoldBarterBehavior.RegisterEvents))]
    static bool Prefix()
    {
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;
        return false;
    }
}
