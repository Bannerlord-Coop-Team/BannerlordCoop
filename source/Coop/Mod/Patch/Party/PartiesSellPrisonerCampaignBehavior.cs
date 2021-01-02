using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.SandBox.CampaignBehaviors;

namespace Coop.Mod.Patch.Party
{
    [HarmonyPatch(typeof(PartiesSellPrisonerCampaignBehavior), "OnSettlementEntered")]
    class PartiesSellPrisonerCampaignBehaviorPatch
    {
        static bool Prefix(ref MobileParty mobileParty, ref Settlement settlement, ref Hero hero)
        {
            if ((CoopServer.Instance?.Persistence?.EntityManager?.PlayerControllerParties) != null)
            {
                // Skip function if mobile party is player controlled
                return !CoopServer.Instance.Persistence.EntityManager.PlayerControllerParties.ContainsValue(mobileParty);
            }
            return true;
        }
    }
}
