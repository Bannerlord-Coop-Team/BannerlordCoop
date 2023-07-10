using Common.Messaging;
using Common.Util;
using GameInterface.Services.MobileParties.Messages;
using GameInterface.Services.MobileParties.Messages.Behavior;
using HarmonyLib;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.MobileParties.Patches
{
    [HarmonyPatch(typeof(EnterSettlementAction))]
    internal class EnterSettlementActionPatches
    {
        public static readonly AllowedInstance<MobileParty> AllowedInstance = new AllowedInstance<MobileParty>();

        [HarmonyPrefix]
        [HarmonyPatch(nameof(EnterSettlementAction.ApplyForParty))]
        private static bool ApplyForPartyPrefix(ref MobileParty mobileParty, ref Settlement settlement)
        {
            if (AllowedInstance.IsAllowed(mobileParty)) return true;

            var message = new PartyEnterSettlementAttempted(mobileParty.StringId, settlement.StringId);
            MessageBroker.Instance.Publish(mobileParty, message);

            return false;
        }

        public static void OverrideApplyForParty(MobileParty mobileParty, Settlement settlement)
        {
            using (AllowedInstance)
            {
                AllowedInstance.Instance = mobileParty;
                EnterSettlementAction.ApplyForParty(mobileParty, settlement);
            }
        }
    }
}
