using HarmonyLib;
using SandBox.CampaignBehaviors;

namespace GameInterface.Services.Companions.Patches
{
    [HarmonyPatch(typeof(CompanionRolesCampaignBehavior))]
    internal class DisableCompanionRolesCampaignBehavior
    {
        [HarmonyPatch(nameof(CompanionRolesCampaignBehavior.RegisterEvents))]
        static bool Prefix() => false;
    }
}