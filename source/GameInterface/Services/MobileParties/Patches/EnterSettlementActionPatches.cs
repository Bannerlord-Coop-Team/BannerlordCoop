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
        public static readonly AllowedInstance<MobileParty> AllowedInstance = new AllowedInstance<MobileParty>();

        [HarmonyPrefix]
        [HarmonyPatch(nameof(EnterSettlementAction.ApplyForParty))]
        private static bool ApplyForPartyPrefix(ref MobileParty mobileParty)
        {
            // Only run if server
            if (ModInformation.IsServer) return true;
            // Run if commanded by server
            if (AllowedInstance.IsAllowed(mobileParty)) return true;

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
