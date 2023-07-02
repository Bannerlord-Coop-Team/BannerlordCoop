using HarmonyLib;
using SandBox.CampaignBehaviors;
using TaleWorlds.CampaignSystem.CampaignBehaviors;

namespace GameInterface.Services.Cheats.Patches
{
    [HarmonyPatch(typeof(CompanionsCampaignBehavior))]
    internal class DisableCompanionsCampaignBehavior
    {
        [HarmonyPatch(nameof(CompanionsCampaignBehavior.RegisterEvents))]
        static bool Prefix() => false;
    }
}