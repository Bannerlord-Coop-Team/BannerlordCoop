using HarmonyLib;
using SandBox.CampaignBehaviors;
using TaleWorlds.CampaignSystem.CampaignBehaviors;

namespace GameInterface.Services.Cheats.Patches
{
    [HarmonyPatch(typeof(CompanionRolesCampaignBehavior))]
    
    internal class DisableCompanionRolesCampaignBehavior
    {
        [HarmonyPatch(nameof(CompanionRolesCampaignBehavior.RegisterEvents))]
        static bool Prefix() => false;
    }
}