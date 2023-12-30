using GameInterface.Services.MobileParties.Extensions;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Text;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.Settlements.Patches
{
    [HarmonyPatch(typeof(Settlement))]
    internal class SettlementEncounterPatch
    {
        [HarmonyPrefix]
        [HarmonyPatch(nameof(Settlement.OnPartyInteraction))]
        internal static bool HandleEncounterForMobilePartyPatch(ref MobileParty mobileParty)
        {
            // Skip this method if party is not controlled
            if (mobileParty.IsPartyControlled() == false) return false;

            return true;
        }
    }
}
