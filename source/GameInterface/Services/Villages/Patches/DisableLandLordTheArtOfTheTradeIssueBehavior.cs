using HarmonyLib;
using TaleWorlds.CampaignSystem.Issues;
using GameInterface.Policies;

namespace GameInterface.Services.Villages.Patches;

[HarmonyPatch(typeof(LandLordTheArtOfTheTradeIssueBehavior))]
internal class DisableLandLordTheArtOfTheTradeIssueBehavior
{
    [HarmonyPatch(nameof(LandLordTheArtOfTheTradeIssueBehavior.RegisterEvents))]
    static bool Prefix()
    {
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;
        return false;
    }
}
