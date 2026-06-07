using HarmonyLib;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using GameInterface.Policies;

namespace GameInterface.Services.Heroes.Patches;

[HarmonyPatch(typeof(PlayerTrackCompanionBehavior))]
internal class DisablePlayerTrackCompanionBehavior
{
    [HarmonyPatch(nameof(PlayerTrackCompanionBehavior.RegisterEvents))]
    static bool Prefix()
    {
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;
        return false;
    }
}
