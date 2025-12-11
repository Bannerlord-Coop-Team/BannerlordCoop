using Common;
using Common.Messaging;
using Common.Logging;
using Serilog;
using Common.Util;
using GameInterface.Policies;
using GameInterface.Services.MobileParties.Messages.Behavior;
using GameInterface.Services.MobileParties.Extensions;
using HarmonyLib;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.MobileParties.Patches
{
    [HarmonyPatch(typeof(EnterSettlementAction))]
    internal class EnterSettlementActionPatches
    {
        private static ILogger Logger = LogManager.GetLogger<EnterSettlementActionPatches>();
        [HarmonyPrefix]
        [HarmonyPatch(nameof(EnterSettlementAction.ApplyForParty))]
        private static bool ApplyForPartyPrefix(ref MobileParty mobileParty, ref Settlement settlement)
        {
            Logger.Information(
                "EnterSettlementAction.ApplyForParty intercept party={partyId} settlement={settlementId} partyNull={partyNull} settlementPartyNull={settlementNull} current={current}",
                mobileParty.StringId,
                settlement.StringId,
                mobileParty.Party == null,
                settlement.Party == null,
                mobileParty.CurrentSettlement?.StringId ?? "none");
            if (mobileParty.IsPlayerParty() == false) return true;
            if (mobileParty.CurrentSettlement == settlement) return true;

            if (CallOriginalPolicy.IsOriginalAllowed()) return true;

            var message = new StartSettlementEncounterAttempted(mobileParty.StringId, settlement.StringId);
            MessageBroker.Instance.Publish(mobileParty, message);

            return false;
        }

        public static void OverrideApplyForParty(MobileParty mobileParty, Settlement settlement)
        {
            Logger.Information(
                "Override EnterSettlementAction.ApplyForParty party={partyId} settlement={settlementId}",
                mobileParty.StringId,
                settlement.StringId);
            GameLoopRunner.RunOnMainThread(() =>
            {
                using (new AllowedThread())
                {
                    EnterSettlementAction.ApplyForParty(mobileParty, settlement);
                }
            }, blocking: true);
            Logger.Information(
                "Override EnterSettlementAction.ApplyForParty terminé party={partyId} settlement={settlementId} current={current}",
                mobileParty.StringId,
                settlement.StringId,
                mobileParty.CurrentSettlement?.StringId ?? "none");
        }
    }
}
