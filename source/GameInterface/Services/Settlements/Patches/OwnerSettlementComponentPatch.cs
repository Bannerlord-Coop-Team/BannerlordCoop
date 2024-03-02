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
using TaleWorlds.CampaignSystem.Party;
using GameInterface.Extentions;

namespace GameInterface.Services.Settlements.Patches
{
    /// <summary>
    /// Patch for <see cref="SettlementComponent.Owner"/>
    /// </summary>
    [HarmonyPatch(typeof(SettlementComponent))]
    public static class OwnerSettlementComponentPatch
    {
        private static ILogger Logger = LogManager.GetLogger<SettlementComponent>();
        [HarmonyPatch(nameof(SettlementComponent.Owner), MethodType.Setter)]
        public static bool Prefix(SettlementComponent __instance, PartyBase value)
        {
            if (CallOriginalPolicy.IsOriginalAllowed()) return true;

            if (ModInformation.IsClient)
            {
                Logger.Error("Client created unmanaged {name}\n"
                    + "Callstack: {callstack}", typeof(SettlementComponent), Environment.StackTrace);
                return false;
            }

            var message = new SettlementComponentOwnerChanged(__instance.StringId, value.Id);

            MessageBroker.Instance.Publish(__instance, message);
            return true;
        }

        internal static void RunSettlementComponentOwnerChanged(SettlementComponent settlementComp, PartyBase owner)
        {
            GameLoopRunner.RunOnMainThread(() =>
            {
                using (new AllowedThread())
                {
                    settlementComp.Owner = owner;
                }
            });
        }
    }
}
