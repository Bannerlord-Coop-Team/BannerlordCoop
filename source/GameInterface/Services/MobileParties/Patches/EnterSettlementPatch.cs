using Common.Messaging;
using Common.Util;
using Common;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Text;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.Party;
using GameInterface.Services.MobileParties.Messages;

namespace GameInterface.Services.MobileParties.Patches
{
    /// <summary>
    /// Patch when the local player enters a settlement.
    /// </summary>
    [HarmonyPatch(typeof(PlayerEncounter), "EnterSettlement")]
    public class EnterSettlementPatch
    {
        static bool Prefix()
        {
            var message = new SettlementEntered(MobileParty.MainParty.TargetSettlement.StringId, MobileParty.MainParty.StringId);
            MessageBroker.Instance.Publish(MobileParty.MainParty, message);

            return true;
        }
    }
}
