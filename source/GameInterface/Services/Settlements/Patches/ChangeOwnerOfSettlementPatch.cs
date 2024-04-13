using Common;
using Common.Extensions;
using Common.Messaging;
using Common.Util;
using GameInterface.Policies;
using GameInterface.Services.Settlements.Messages;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Library.NewsManager;
using static TaleWorlds.CampaignSystem.Actions.ChangeOwnerOfSettlementAction;

namespace GameInterface.Services.Settlements.Patches
{
    /// <summary>
    /// Patches ownership changes of settlements
    /// </summary>
    [HarmonyPatch(typeof(ChangeOwnerOfSettlementAction), "ApplyInternal")]
    public class ChangeOwnerOfSettlementPatch
    {
        public static bool Prefix(Settlement settlement, Hero newOwner, Hero capturerHero, ChangeOwnerOfSettlementDetail detail)
        {
            if (CallOriginalPolicy.IsOriginalAllowed()) return true;

            MessageBroker.Instance.Publish(settlement, 
                new LocalSettlementOwnershipChange(settlement.StringId, newOwner?.StringId, capturerHero?.StringId, Convert.ToInt32(detail)));

            return false;
        }
    
        public static void RunOriginalApplyInternal(Settlement settlement, Hero newOwner, Hero capturerHero, ChangeOwnerOfSettlementDetail detail)
        {
            GameLoopRunner.RunOnMainThread(() =>
            {
                using (new AllowedThread())
                {
                    ChangeOwnerOfSettlementAction.ApplyInternal(settlement, newOwner, capturerHero, detail);
                }
            }, true);
        }
    }
}
