using Common;
using Common.Logging;
using Common.Messaging;
using Common.Util;
using GameInterface.Policies;
using GameInterface.Services.Settlements.Messages;
using HarmonyLib;
using Serilog;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

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
                Logger.Error("Client created managed {name}", typeof(SettlementComponent));
                return false;
            }

            var message = new SettlementComponentOwnerChanged(__instance, value.Id);

            MessageBroker.Instance.Publish(__instance, message);
            return true;
        }

        internal static void RunSettlementComponentOwnerChanged(SettlementComponent settlementComp, PartyBase owner)
        {
            GameThread.Run(() =>
            {
                using (new AllowedThread())
                {
                    settlementComp.Owner = owner;
                }
            });
        }
    }
}
