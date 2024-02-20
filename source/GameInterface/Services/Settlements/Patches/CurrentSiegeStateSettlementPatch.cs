using HarmonyLib;
using TaleWorlds.CampaignSystem.Settlements;
using static TaleWorlds.CampaignSystem.Settlements.Settlement;
using Common.Util;
using GameInterface.Policies;
using GameInterface.Services.Settlements.Messages;
using Common.Messaging;
using Common;
using System.Reflection;
using GameInterface.Extentions;

namespace GameInterface.Services.Settlements.Patches;

/// <summary>
/// Used to handle CurrentSiegeStatus
/// </summary>

[HarmonyPatch(typeof(Settlement))]
public class CurrentSiegeStateSettlementPatch
{

    [HarmonyPatch(nameof(Settlement.CurrentSiegeState), MethodType.Setter)]
    [HarmonyPrefix]
    private static bool CurrentSiegeStatePrefix(ref Settlement __instance, ref SiegeState value)
    {
        if (AllowedThread.IsThisThreadAllowed()) return true;
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;

        if (ModInformation.IsClient) return false;

        var message = new SettlementChangedCurrentSiegeState(__instance.StringId, (short)value);

        MessageBroker.Instance.Publish(__instance, message);
        return true;
    }

    internal static void RunCurrentSiegeStateChange(Settlement settlement, SiegeState siegeState)
    {
        GameLoopRunner.RunOnMainThread(() =>
        {
            using (new AllowedThread())
            {
                settlement.SetSiegeState(siegeState);
            }
        });
    }
}
