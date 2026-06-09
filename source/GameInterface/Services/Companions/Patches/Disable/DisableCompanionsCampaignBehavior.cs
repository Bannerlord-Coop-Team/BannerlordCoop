using Common;
using HarmonyLib;
using TaleWorlds.CampaignSystem.CampaignBehaviors;

namespace GameInterface.Services.Companions.Patches.Disable
{
    [HarmonyPatch(typeof(CompanionsCampaignBehavior))]
    internal class DisableCompanionsCampaignBehavior
    {
        [HarmonyPatch(nameof(CompanionsCampaignBehavior.RegisterEvents))]
        static bool Prefix() => ModInformation.IsServer;
    }
}