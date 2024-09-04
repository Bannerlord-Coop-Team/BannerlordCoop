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
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.Settlements.Patches;

/// <summary>
/// Sync LastAttackerParty
/// </summary>
[HarmonyPatch(typeof(Settlement))]
public class LastAttackerPartySettlementPatch
{

    private static ILogger Logger = LogManager.GetLogger<Settlement>();

    [HarmonyPatch(nameof(Settlement.LastAttackerParty), MethodType.Setter)]
    [HarmonyPrefix]
    private static bool LastAttackerPartyPrefix(ref Settlement __instance, ref MobileParty value)
    {
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;

        if (ModInformation.IsClient)
        {
            Logger.Error("Client created unmanaged {name}\n"
                + "Callstack: {callstack}", typeof(Settlement), Environment.StackTrace);
            return true;
        }

        if (value == null) return true;

        if (__instance.LastAttackerParty != null) // last attacker party can be null
        {
            if (__instance.LastAttackerParty.StringId == value.StringId) return false;
        }

        var message = new SettlementChangedLastAttackerParty(__instance.StringId, value.StringId);

        MessageBroker.Instance.Publish(__instance, message);

        return true;
    }

    internal static void RunLastAttackerPartyChange(Settlement settlement, MobileParty mobileParty)
    {
        GameLoopRunner.RunOnMainThread(() =>
        {
            using (new AllowedThread())
            {
               settlement.LastAttackerParty = mobileParty;
            }
        });
    }
}
