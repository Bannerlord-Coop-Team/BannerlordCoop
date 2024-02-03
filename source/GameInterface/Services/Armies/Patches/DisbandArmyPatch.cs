using Common;
using Common.Messaging;
using Common.Util;
using GameInterface.Policies;
using GameInterface.Services.Armies.Extensions;
using GameInterface.Services.Armies.Messages;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.Armies.Patches
{
    [HarmonyPatch(typeof(DisbandArmyAction))]
    internal class DisbandArmyPatch
    {

        [HarmonyPatch("ApplyInternal")]
        [HarmonyPrefix]
        public static bool DisbandArmyApplyInternal(ref Army army, Army.ArmyDispersionReason reason)
        {
            if(AllowedThread.IsThisThreadAllowed()) { return true; }
            if (PolicyProvider.AllowOriginalCalls) { return true; }
            if (ModInformation.IsClient) { return false; }

            MessageBroker.Instance.Publish(army, new ArmyDisbanded(army.GetStringId(), reason.ToString()));
            return true;
        }

        public static void DisbandArmy(Army army, Army.ArmyDispersionReason reason)
        {
            GameLoopRunner.RunOnMainThread(() =>
            {
                using (new AllowedThread())
                {
                    DisbandArmyActionExtension.ApplyInternal(army, reason);
                }
            });

        }

    }






   
}
