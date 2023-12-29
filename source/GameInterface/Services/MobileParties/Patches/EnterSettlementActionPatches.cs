using Common.Messaging;
using Common.Util;
using GameInterface.Services.GameDebug.Patches;
using GameInterface.Services.MobileParties.Messages;
using GameInterface.Services.MobileParties.Messages.Behavior;
using HarmonyLib;
using System;
using System.Runtime.CompilerServices;
using System.Threading;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.MobileParties.Patches
{
    [HarmonyPatch(typeof(EnterSettlementAction))]
    internal class EnterSettlementActionPatches
    {
        public static AllowedInstance<MobileParty> AllowedInstance = new AllowedInstance<MobileParty>();

        [HarmonyPrefix]
        [HarmonyPatch(nameof(EnterSettlementAction.ApplyForParty))]
        private static bool ApplyForPartyPrefix(ref MobileParty mobileParty, ref Settlement settlement)
        {
            if (mobileParty.CurrentSettlement == settlement) return false;

            CallStackValidator.Validate(mobileParty, AllowedInstance);

            if (AllowedInstance.IsAllowed(mobileParty)) return true;

            var message = new PartyEnterSettlementAttempted(settlement.StringId, mobileParty.StringId);
            MessageBroker.Instance.Publish(mobileParty, message);

            return false;
        }

        public static void OverrideApplyForParty(MobileParty mobileParty, Settlement settlement)
        {
            using(AllowedInstance)
            {
                AllowedInstance.Instance = mobileParty;
                EnterSettlementAction.ApplyForParty(mobileParty, settlement);
            }
        }
    }
}


