using HarmonyLib;
using SandBox.CampaignBehaviors;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using GameInterface.Policies;

namespace GameInterface.Services.Cheats.Patches
{
    [HarmonyPatch(typeof(CompanionsCampaignBehavior))]
    internal class DisableCompanionsCampaignBehavior
    {
        [HarmonyPatch(nameof(CompanionsCampaignBehavior.RegisterEvents))]
        static bool Prefix()
        {
            if (CallOriginalPolicy.IsOriginalAllowed()) return true;
            return false;
        }
    }
}