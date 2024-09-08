using Common;
using Common.Logging;
using Common.Messaging;
using Common.Util;
using GameInterface.Policies;
using GameInterface.Services.Settlements.Messages;
using HarmonyLib;
using Serilog;
using System;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.Settlements.Patches;

/// <summary>
/// When the Militia Is Set
/// </summary>
[HarmonyPatch(typeof(Settlement))]
public class MilitiaSettlementPatch
{
    private static ILogger Logger = LogManager.GetLogger<Settlement>();

    [HarmonyPatch(nameof(Settlement.Militia), MethodType.Setter)]
    [HarmonyPrefix]
    private static bool MilitiaPrefix(ref Settlement __instance, ref float value)
    {
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;

        if (ModInformation.IsClient)
        {
            Logger.Error("Client created unmanaged {name}\n"
                + "Callstack: {callstack}", typeof(Settlement), Environment.StackTrace);
            return true;
        }

        var message = new SettlementChangedMilitia(__instance.StringId, value);

        MessageBroker.Instance.Publish(__instance, message);
        return true;
    }

    internal static void RunMiltiiaChange(Settlement settlement, float militia)
    {
        GameLoopRunner.RunOnMainThread(() =>
        {
            using (new AllowedThread())
            {
                settlement._readyMilitia = militia;
            }
        });
    }
}
