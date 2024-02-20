using Common;
using Common.Messaging;
using Common.Util;
using GameInterface.Policies;
using GameInterface.Services.Settlements.Messages;
using HarmonyLib;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.Settlements.Patches;

/// <summary>
/// Sync LastAttackerParty
/// </summary>
[HarmonyPatch(typeof(Settlement))]
public class LastAttackerPartySettlementPatch
{

    [HarmonyPatch(nameof(Settlement.LastAttackerParty), MethodType.Setter)]
    [HarmonyPrefix]
    private static bool LastAttackerPartyPrefix(ref Settlement __instance, ref MobileParty value)
    {
        if (AllowedThread.IsThisThreadAllowed()) return true;
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;

        if (ModInformation.IsClient) return false;

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
