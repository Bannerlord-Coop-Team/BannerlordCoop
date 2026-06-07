using HarmonyLib;
using TaleWorlds.CampaignSystem.CampaignBehaviors.BarterBehaviors;
using GameInterface.Policies;

namespace GameInterface.Services.Barters.Patches;

[HarmonyPatch(typeof(ItemBarterBehavior))]
internal class DisableItemBarterBehavior
{
    [HarmonyPatch(nameof(ItemBarterBehavior.RegisterEvents))]
    static bool Prefix()
    {
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;
        return false;
    }
}
