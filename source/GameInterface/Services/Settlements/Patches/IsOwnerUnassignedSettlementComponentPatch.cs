using Common.Logging;
using Common.Messaging;
using Common;
using GameInterface.Policies;
using GameInterface.Services.Settlements.Messages;
using HarmonyLib;
using Serilog;
using System;
using System.Collections.Generic;
using System.Text;
using TaleWorlds.CampaignSystem.Settlements;
using Common.Util;

namespace GameInterface.Services.Settlements.Patches
{
    /// <summary>
    /// Patch for <see cref="SettlementComponent.IsOwnerUnassigned"/>
    /// </summary>
    [HarmonyPatch(typeof(SettlementComponent))]
    public static class IsOwnerUnassignedSettlementComponentPatch
    {
        private static ILogger Logger = LogManager.GetLogger<SettlementComponent>();
        [HarmonyPatch(nameof(SettlementComponent.IsOwnerUnassigned), MethodType.Setter)]
        public static bool Prefix(SettlementComponent __instance, bool value)
        {
            if (CallOriginalPolicy.IsOriginalAllowed()) return true;

            if (ModInformation.IsClient)
            {
                Logger.Error("Client created unmanaged {name}\n"
                    + "Callstack: {callstack}", typeof(SettlementComponent), Environment.StackTrace);
                return false;
            }

            var message = new SettlementComponentChangedIsOwnerUnassigned(__instance.StringId, value);

            MessageBroker.Instance.Publish(__instance, message);
            return true;
        }

        internal static void RunSettlementComponentIsOwnerUnassignedChanged(SettlementComponent settlementComp, bool value)
        {
            GameLoopRunner.RunOnMainThread(() =>
            {
                using (new AllowedThread())
                {
                    settlementComp.IsOwnerUnassigned = value;
                }
            });
        }
    }
}
