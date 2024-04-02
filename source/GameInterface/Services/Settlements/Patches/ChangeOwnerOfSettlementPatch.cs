using Common;
using Common.Extensions;
using Common.Messaging;
using Common.Util;
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
        private static readonly AllowedInstance<Settlement> AllowedInstance = new AllowedInstance<Settlement>();

        private static readonly Action<Settlement, Hero, Hero, ChangeOwnerOfSettlementAction.ChangeOwnerOfSettlementDetail> ApplyInternal = 
        typeof(ChangeOwnerOfSettlementAction)
        .GetMethod("ApplyInternal", BindingFlags.NonPublic | BindingFlags.Static)
        .BuildDelegate<Action<Settlement, Hero, Hero, ChangeOwnerOfSettlementAction.ChangeOwnerOfSettlementDetail>>();
    
    {    
        public static bool Prefix(Settlement settlement, Hero newOwner, Hero capturerHero, ChangeOwnerOfSettlementDetail detail)
        {
            if (AllowedInstance.IsAllowed(settlement)) return true;

            MessageBroker.Instance.Publish(settlement, 
                new LocalSettlementOwnershipChange(settlement.StringId, newOwner?.StringId, capturerHero?.StringId, Convert.ToInt32(detail)));

            return false;
        }
    
        public static void RunOriginalApplyInternal(Settlement settlement, Hero newOwner, Hero capturerHero, ChangeOwnerOfSettlementDetail detail)
        {
            using (AllowedInstance)
            {
                AllowedInstance.Instance = settlement;

                GameLoopRunner.RunOnMainThread(() =>
                {
                    ApplyInternal.Invoke(settlement, newOwner, capturerHero, detail);
                }, true);
            }
        }
    
    }
}
