using Common;
using Common.Messaging;
using Common.Util;
using GameInterface.Policies;
using GameInterface.Services.Settlements.Messages;
using HarmonyLib;
using System.Reflection;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.Settlements.Patches;


/// <summary>
/// Sync the SettlementHitPoints
/// </summary>
[HarmonyPatch(typeof(Settlement))]
internal class SettlementHitPointsPatch
{
    private static readonly PropertyInfo SettlementHitPoints = typeof(Settlement).GetProperty(nameof(Settlement.SettlementHitPoints));

    [HarmonyPatch(nameof(Settlement.SettlementHitPoints), MethodType.Setter)]
    [HarmonyPrefix]
    private static bool SettlementHitPointsPrefix(ref Settlement __instance, ref float value)
    {
        if (AllowedThread.IsThisThreadAllowed()) return true;
        if (PolicyProvider.AllowOriginalCalls) return true;

        if (ModInformation.IsClient) return false;

        if (__instance.SettlementHitPoints == value) return false;

        var message = new SettlementChangedSettlementHitPoints(__instance.StringId, value);

        MessageBroker.Instance.Publish(__instance, message);

        return true;
    }

    internal static void RunSettlementHitPointsChange(Settlement settlement, float settlementHitPoints)
    {
        GameLoopRunner.RunOnMainThread(() =>
        {
            using (new AllowedThread())
            {
                SettlementHitPoints.SetValue(settlement, settlementHitPoints);  
            }
        });
    }
}
