using Common.Messaging;
using Common.Util;
using Common;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Text;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.MapEvents.Patches
{
    public class EnterSettlementPatch
    {
        [HarmonyPatch(typeof(PlayerEncounter), "EnterSettlement")]
        [HarmonyPrefix]
        private static bool EnterSettlementPrefix()
        {
            MessageBroker.Instance.Publish(MobileParty.MainParty, new SettlementEntered(MobileParty.MainParty.TargetSettlement.StringId, MobileParty.MainParty.StringId));

            return true;
        }
    }
}
