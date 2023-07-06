using Common.Messaging;
using Common.Util;
using GameInterface.Services.MobileParties.Messages;
using HarmonyLib;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.MobileParties.Patches
{
    [HarmonyPatch(typeof(EnterSettlementAction))]
    internal class EnterSettlementActionPatches
    {
        private static AllowedInstance<MobileParty> allowedInstance = new AllowedInstance<MobileParty>();

        [HarmonyPrefix]
        [HarmonyPatch(nameof(EnterSettlementAction.ApplyForParty))]
        private static bool ApplyForPartyPrefix(ref MobileParty mobileParty, ref Settlement settlement)
        {
            if(allowedInstance?.Instance == mobileParty) return true;

            MessageBroker.Instance.Publish(mobileParty, new SettlementEntered(settlement.StringId, mobileParty.StringId));

            return false;
        }

        public static void OverrideApplyForParty(MobileParty mobileParty, Settlement settlement)
        {
            using (allowedInstance)
            {
                allowedInstance.Instance = mobileParty;
                EnterSettlementAction.ApplyForParty(mobileParty, settlement);
            }
        }
    }
}
