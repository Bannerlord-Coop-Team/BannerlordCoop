using Common;
using HarmonyLib;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.Companions.Patches
{
    [HarmonyPatch(typeof(CompanionsCampaignBehavior))]
    internal class DisableCompanionsCampaignBehavior
    {
        [HarmonyPatch(nameof(CompanionsCampaignBehavior.RegisterEvents))]
        static bool Prefix() => ModInformation.IsServer;

        [HarmonyPatch(nameof(CompanionsCampaignBehavior.CreateCompanionAndAddToSettlement))]
        [HarmonyPostfix]
        public static void CreateCompanionAndAddToSettlementPostfix(Settlement settlement)
        {
            return;
        }
    }
}