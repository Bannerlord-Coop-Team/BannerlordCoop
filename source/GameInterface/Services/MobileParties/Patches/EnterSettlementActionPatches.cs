using Common.Util;
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
        private static bool ApplyForPartyPrefix(ref MobileParty mobileParty)
        {
            if(allowedInstance?.Instance == mobileParty) return true;

            return false;
        }

        public static void OverrideApplyForParty(ref MobileParty mobileParty, ref Settlement settlement)
        {
            using (allowedInstance)
            {
                allowedInstance.Instance = mobileParty;
                EnterSettlementAction.ApplyForParty(mobileParty, settlement);
            }
        }
    }
}
