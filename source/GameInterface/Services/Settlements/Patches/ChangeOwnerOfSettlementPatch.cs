using Common;
using Common.Logging;
using Common.Messaging;
using Common.Util;
using GameInterface.Policies;
using GameInterface.Services.Settlements.Messages;
using HarmonyLib;
using Serilog;
using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Settlements;
using static TaleWorlds.CampaignSystem.Actions.ChangeOwnerOfSettlementAction;

namespace GameInterface.Services.Settlements.Patches
{
    /// <summary>
    /// Patches ownership changes of settlements
    /// </summary>
    [HarmonyPatch(typeof(ChangeOwnerOfSettlementAction), "ApplyInternal")]
    public class ChangeOwnerOfSettlementPatch
    {
        static readonly ILogger Logger = LogManager.GetLogger<ChangeOwnerOfSettlementPatch>();

        public static bool Prefix(Settlement settlement, Hero newOwner, Hero capturerHero, ChangeOwnerOfSettlementDetail detail)
        {
            if (CallOriginalPolicy.IsOriginalAllowed()) return true;

            if (ModInformation.IsClient)
            {
                Logger.Error("Client called unmanaged {name}\n"
                    + "Callstack: {callstack}", typeof(ChangeOwnerOfSettlementAction), Environment.StackTrace);
                return false;
            }

            MessageBroker.Instance.Publish(settlement, 
                new SettlementOwnershipChanged(
                    settlement.StringId, 
                    newOwner?.StringId, 
                    capturerHero?.StringId, 
                    Convert.ToInt32(detail)));

            RunOriginalApplyInternal(settlement, newOwner, capturerHero,detail);
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
            });
        }
    
    }
}
