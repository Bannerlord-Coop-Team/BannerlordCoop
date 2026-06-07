using HarmonyLib;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using GameInterface.Policies;

namespace GameInterface.Services.MobileParties.Patches;

[HarmonyPatch(typeof(MobilePartyTrainingBehavior))]
internal class DisableMobilePartyTrainingBehavior
{
    [HarmonyPatch(nameof(MobilePartyTrainingBehavior.RegisterEvents))]
    static bool Prefix()
    {
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;
        return false;
    }
}
