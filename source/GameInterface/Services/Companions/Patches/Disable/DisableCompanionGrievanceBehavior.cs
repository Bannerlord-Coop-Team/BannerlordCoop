using HarmonyLib;
using TaleWorlds.CampaignSystem.CampaignBehaviors;

namespace GameInterface.Services.Companions.Patches.Disable
{
    [HarmonyPatch(typeof(CompanionGrievanceBehavior))]
    internal class DisableCompanionGrievanceBehavior
    {
        [HarmonyPatch(nameof(CompanionGrievanceBehavior.RegisterEvents))]
        static bool Prefix() => false;
    }
}