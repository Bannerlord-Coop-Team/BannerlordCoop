using HarmonyLib;
using SandBox.CampaignBehaviors;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using GameInterface.Policies;

namespace GameInterface.Services.Cheats.Patches
{
    [HarmonyPatch(typeof(CompanionRolesCampaignBehavior))]
    
    internal class DisableCompanionRolesCampaignBehavior
    {
        [HarmonyPatch(nameof(CompanionRolesCampaignBehavior.RegisterEvents))]
        static bool Prefix()
        {
            if (CallOriginalPolicy.IsOriginalAllowed()) return true;
            return false;
        }
    }
}