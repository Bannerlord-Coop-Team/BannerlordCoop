using Common;
using Common.Messaging;
using Common.Util;
using GameInterface.Policies;
using GameInterface.Services.Settlements.Messages;
using HarmonyLib;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.Settlements.Patches;

/// <summary>
/// When the Militia Is Set
/// </summary>
[HarmonyPatch(typeof(Settlement))]
public class MilitiaSettlementPatch
{
    [HarmonyPatch(nameof(Settlement.Militia), MethodType.Setter)]
    [HarmonyPrefix]
    private static bool MilitiaPrefix(ref Settlement __instance, ref float value)
    {
        if (AllowedThread.IsThisThreadAllowed()) return true;
        if (PolicyProvider.AllowOriginalCalls) return true;

        if (ModInformation.IsClient) return false;


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
                settlement.Militia = militia;
            }
        });
    }
}
