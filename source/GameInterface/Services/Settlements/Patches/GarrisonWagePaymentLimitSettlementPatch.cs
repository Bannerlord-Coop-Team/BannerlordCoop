using Common;
using Common.Logging;
using Common.Messaging;
using Common.Util;
using GameInterface.Policies;
using GameInterface.Registry.Auto;
using GameInterface.Services.Settlements.Messages;
using HarmonyLib;
using Serilog;
using System;
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
            ClientCallSites.Log(Logger,
                "Client changed managed {Settlement} GarrisonWagePaymentLimit to {Value}; caller={Caller}", new object[] { __instance.StringId, value },
                "Client changed managed GarrisonWagePaymentLimit {Count} more times from callers already logged");
            return true;
        }


        var message = new SettlementChangedGarrisonWageLimit(__instance, value);

        MessageBroker.Instance.Publish(__instance, message);

        return true;

    }

    // Garrison code (vanilla, or mods like Improved Garrisons) can set the wage limit many times per second
    // on a client, so an error per call floods the log. The first call from each caller is logged with the
    // stack trace and repeats are summed periodically.
    private static readonly CallSiteLogLimiter ClientCallSites = new(64, TimeSpan.FromSeconds(10));

    //client side sync TaleWorlds.CampaignSystem.ViewModelCollection.ClanManagement.ClanFinanceExpenseItemVM.OnCurrentWageLimitUpdated(int) : void @0600185A
    //client side sync TaleWorlds.CampaignSystem.ViewModelCollection.ClanManagement.ClanFinanceExpenseItemVM.OnUnlimitedWageToggled(bool) : void @0600185B

    internal static void RunGarrisonWagePaymentLimitChange(Settlement settlement, int garrisonWagePaymentLimit)
    {
        GameThread.Run(() =>
        {
            using (new AllowedThread())
            {
                settlement.SetGarrisonWagePaymentLimit(garrisonWagePaymentLimit);
            }
        });
    }
}
