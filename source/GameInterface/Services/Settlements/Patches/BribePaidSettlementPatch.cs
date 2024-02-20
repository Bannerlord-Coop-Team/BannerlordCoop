using Common;
using Common.Messaging;
using Common.Util;
using GameInterface.Policies;
using GameInterface.Services.Settlements.Messages;
using HarmonyLib;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.Settlements.Patches;


/// <summary>
/// Used to sync the Settlement.BribePaid value.
/// </summary>
[HarmonyPatch(typeof(Settlement))]
internal class BribePaidSettlementPatch
{
    [HarmonyPatch(nameof(Settlement.BribePaid),MethodType.Setter)]
    [HarmonyPrefix]
    private static bool BribePaidPrefix(ref Settlement __instance, ref int value)
    {
        if (AllowedThread.IsThisThreadAllowed()) return true;
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;
        if (ModInformation.IsClient) return false;

        if (__instance.BribePaid == value) return false;


        var message = new SettlementChangedBribePaid(__instance.StringId, value);

        MessageBroker.Instance.Publish(__instance, message);

        return true;
    }

    internal static void RunBribePaidChange(Settlement settlement, int bribePaid)
    {
        GameLoopRunner.RunOnMainThread(() =>
        {
            using (new AllowedThread())
            {
                settlement.BribePaid = bribePaid;
            }
        });
    }

}
