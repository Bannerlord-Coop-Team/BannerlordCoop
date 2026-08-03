using Common;
using Common.Messaging;
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
            // Idempotency first, and it must stay first: SettlementInterface.PartyEnterSettlement
            // applies entries under an AllowedThread, so letting an allowed original past this check
            // makes a party already inside re-enter.
            if (mobileParty == null || mobileParty.CurrentSettlement == settlement) return false;

            // An explicitly allowed original then wins over the map-event veto below. Without this,
            // a caller that deliberately opened an AllowedThread - the server applying an
            // authoritative entry, or a client completing a break-in - was silently dropped whenever
            // the party was attached to a map-event side, which is the normal case during a siege.
            if (CallOriginalPolicy.IsOriginalAllowed()) return true;

            if (mobileParty.Party?.MapEventSide != null) return false;

            var message = new PartyEnterSettlementAttempted(settlement, mobileParty);
            MessageBroker.Instance.Publish(mobileParty, message);

            return ModInformation.IsServer;
        }
    }
}
