using Common;
using Common.Extensions;
using Common.Messaging;
using Common.Util;
using GameInterface.Policies;
using GameInterface.Services.Armies.Extensions;
using GameInterface.Services.Armies.Messages;
using GameInterface.Services.Fiefs.Messages;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.Armies.Patches
{
    [HarmonyPatch(typeof(Army), "OnAddPartyInternal")]
    public class ArmyPatches
    {
        private static Action<Army, MobileParty> OnAddPartyInternal = typeof(Army).GetMethod("OnAddPartyInternal", BindingFlags.NonPublic | BindingFlags.Instance)
        .BuildDelegate<Action<Army, MobileParty>>();


        static bool Prefix(ref Army __instance, MobileParty mobileParty)
        {
            if (AllowedThread.IsThisThreadAllowed()) return true;
            if (PolicyProvider.AllowOriginalCalls) return true;

            if (ModInformation.IsClient) return false;

            var message = new MobilePartyInArmyAdded(mobileParty.StringId, __instance.LeaderParty.StringId);
            MessageBroker.Instance.Publish(mobileParty, message);

            return true;
        }


        public static void AddMobilePartyInArmy(MobileParty mobileParty, Army army)
        {
            GameLoopRunner.RunOnMainThread(() =>
            {
                using (new AllowedThread())
                {
                    ArmyExtensions.OnAddPartyInternal(mobileParty, army);
                }
            });

        }
    }
}




