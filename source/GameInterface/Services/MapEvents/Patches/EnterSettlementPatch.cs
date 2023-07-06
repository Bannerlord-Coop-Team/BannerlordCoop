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
    [HarmonyPatch(typeof(PlayerEncounter), "EnterSettlement")]
    public class EnterSettlementPatch
    {
        static bool Prefix()
        {
            MessageBroker.Instance.Publish(MobileParty.MainParty, new SettlementEntered(MobileParty.MainParty.TargetSettlement.StringId, MobileParty.MainParty.StringId));

            return true;
        }
    }
}
