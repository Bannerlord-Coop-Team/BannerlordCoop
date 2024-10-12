using Common;
using Common.Messaging;
using Common.Util;
using GameInterface.Policies;
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
        [HarmonyPrefix]
        [HarmonyPatch(nameof(EnterSettlementAction.ApplyForParty))]
        private static bool ApplyForPartyPrefix(ref MobileParty mobileParty, ref Settlement settlement)
        {
            if (mobileParty.CurrentSettlement == settlement) return false;

            if (CallOriginalPolicy.IsOriginalAllowed()) return true;

            var message = new PartyEnterSettlementAttempted(settlement.StringId, mobileParty.StringId);
            MessageBroker.Instance.Publish(mobileParty, message);

            return false;
        }

        public static void OverrideApplyForParty(MobileParty mobileParty, Settlement settlement)
        {
            GameLoopRunner.RunOnMainThread(() =>
            {
                using (new AllowedThread())
                {
                    EnterSettlementAction.ApplyForParty(mobileParty, settlement);
                }
            }, blocking: true);
        }
    }
}


