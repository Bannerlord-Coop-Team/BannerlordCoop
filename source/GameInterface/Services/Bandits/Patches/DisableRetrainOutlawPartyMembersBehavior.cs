using HarmonyLib;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using GameInterface.Policies;

namespace GameInterface.Services.Bandits.Patches;

[HarmonyPatch(typeof(RetrainOutlawPartyMembersBehavior))]
internal class DisableRetrainOutlawPartyMembersBehavior
{
    [HarmonyPatch(nameof(RetrainOutlawPartyMembersBehavior.RegisterEvents))]
    static bool Prefix()
    {
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;
        return false;
    }
}
