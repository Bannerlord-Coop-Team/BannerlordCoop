using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.SandBox.CampaignBehaviors;

namespace Coop.Mod.Patch.Party
{
    [HarmonyPatch(typeof(PartiesSellLootCampaignBehavior), "OnSettlementEntered")]
    internal class PartiesSellLootCampaignBehaviorPatch
    {
        private static bool Prefix(ref MobileParty mobileParty, ref Settlement settlement, ref Hero hero)
        {
            // Skip function if mobile party is player controlled
            return !mobileParty.IsAnyPlayerMainParty();
        }
    }
}