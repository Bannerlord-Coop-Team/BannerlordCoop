using HarmonyLib;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using GameInterface.Policies;

namespace GameInterface.Services.MobileParties.Patches;

[HarmonyPatch(typeof(PartiesSellPrisonerCampaignBehavior))]
internal class DisablePartiesSellPrisonerCampaignBehavior
{
    [HarmonyPatch(nameof(PartiesSellPrisonerCampaignBehavior.RegisterEvents))]
    static bool Prefix()
    {
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;
        return false;
    }
}
