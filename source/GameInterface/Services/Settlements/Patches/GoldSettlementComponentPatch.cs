using Common;
using Common.Logging;
using Common.Messaging;
using Common.Util;
using GameInterface.Extentions;
using GameInterface.Policies;
using GameInterface.Services.Settlements.Messages;
using HarmonyLib;
using Serilog;
using System;
using System.Collections.Generic;
using System.Text;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.Settlements.Patches
{
    /// <summary>
    /// Patch for <see cref="SettlementComponent.Gold"/>
    /// </summary>
    [HarmonyPatch(typeof(SettlementComponent))]
    public static class GoldSettlementComponentPatch
    {
        private static ILogger Logger = LogManager.GetLogger<SettlementComponent>();
        [HarmonyPatch(nameof(SettlementComponent.Gold), MethodType.Setter)]
        public static bool Prefix(SettlementComponent __instance, int value)
        {
            if (CallOriginalPolicy.IsOriginalAllowed()) return true;

            if (ModInformation.IsClient)
            {
                Logger.Error("Client created unmanaged {name}\n"
                    + "Callstack: {callstack}", typeof(SettlementComponent), Environment.StackTrace);
                return false;
            }

            var message = new SettlementComponentChangedGold(__instance.StringId, value);

            MessageBroker.Instance.Publish(__instance, message);
            return true;
        }

        internal static void RunSettlementComponentGoldChanged(SettlementComponent settlementComp, int gold)
        {
            GameLoopRunner.RunOnMainThread(() =>
            {
                using (new AllowedThread())
                {
                    settlementComp.Gold = gold;
                }
            });
        }
    }
}
