using HarmonyLib;
using TaleWorlds.CampaignSystem.CampaignBehaviors.BarterBehaviors;
using GameInterface.Policies;

namespace GameInterface.Services.Barters.Patches;

[HarmonyPatch(typeof(SetPrisonerFreeBarterBehavior))]
internal class DisableSetPrisonerFreeBarterBehavior
{
    [HarmonyPatch(nameof(SetPrisonerFreeBarterBehavior.RegisterEvents))]
    static bool Prefix()
    {
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;
        return false;
    }
}
