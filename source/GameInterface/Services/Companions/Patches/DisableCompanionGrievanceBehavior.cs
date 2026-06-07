using HarmonyLib;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using GameInterface.Policies;

namespace GameInterface.Services.Cheats.Patches
{
    [HarmonyPatch(typeof(CompanionGrievanceBehavior))]
    internal class DisableCompanionGrievanceBehavior
    {
        [HarmonyPatch(nameof(CompanionGrievanceBehavior.RegisterEvents))]
        static bool Prefix()
        {
            if (CallOriginalPolicy.IsOriginalAllowed()) return true;
            return false;
        }
    }
}