using HarmonyLib;
using SandBox.CampaignBehaviors;

namespace GameInterface.Services.Cheats.Patches
{
    [HarmonyPatch(typeof(CompanionRolesCampaignBehavior))]
    internal class DisableCompanionRolesCampaignBehavior
    {
        [HarmonyPatch(nameof(CompanionRolesCampaignBehavior.RegisterEvents))]
        static bool Prefix() => false;
    }
}