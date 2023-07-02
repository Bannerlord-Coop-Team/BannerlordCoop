using HarmonyLib;
using TaleWorlds.CampaignSystem.CampaignBehaviors;

namespace GameInterface.Services.Cheats.Patches
{
    [HarmonyPatch(typeof(CompanionGrievanceBehavior))]
    internal class DisableCompanionGrievanceBehavior
    {
        [HarmonyPatch(nameof(CompanionGrievanceBehavior.RegisterEvents))]
        static bool Prefix() => false;
    }
}