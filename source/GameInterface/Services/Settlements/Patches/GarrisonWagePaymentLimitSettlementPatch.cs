using Common;
using Common.Logging;
using Common.Messaging;
using Common.Util;
using GameInterface.Policies;
using GameInterface.Services.Settlements.Messages;
using HarmonyLib;
using Serilog;
using System;
using System.Collections.Generic;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.Settlements.Patches;

/// <summary>
/// Handle changes for GarrisonWagePaymentLimit
/// </summary>
[HarmonyPatch(typeof(Settlement))]
public class GarrisonWagePaymentLimitSettlementPatch
{

    private static ILogger Logger = LogManager.GetLogger<Settlement>();

    [HarmonyPatch(nameof(Settlement.GarrisonWagePaymentLimit), MethodType.Setter)]
    [HarmonyPrefix]
    private static bool GarrisonWagePaymentLimitPrefix(ref Settlement __instance, ref int value)
    {
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;

        if (ModInformation.IsClient)
        {
            Logger.Error("Client created unmanaged {name}\n"
                + "Callstack: {callstack}", typeof(Army), Environment.StackTrace);
            return true;
        }


        var message = new SettlementChangedGarrisonWageLimit(__instance.StringId, value);

        MessageBroker.Instance.Publish(__instance, message);

        return true;

    }

    //client side sync TaleWorlds.CampaignSystem.ViewModelCollection.ClanManagement.ClanFinanceExpenseItemVM.OnCurrentWageLimitUpdated(int) : void @0600185A
    //client side sync TaleWorlds.CampaignSystem.ViewModelCollection.ClanManagement.ClanFinanceExpenseItemVM.OnUnlimitedWageToggled(bool) : void @0600185B

    internal static void RunGarrisonWagePaymentLimitChange(Settlement settlement, int garrisonWagePaymentLimit)
    {
        GameLoopRunner.RunOnMainThread(() =>
        {
            using (new AllowedThread())
            {
                settlement.SetGarrisonWagePaymentLimit(garrisonWagePaymentLimit);
            }
        });
    }
}
