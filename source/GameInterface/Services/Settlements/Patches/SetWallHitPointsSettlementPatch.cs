using Common;
using Common.Logging;
using Common.Messaging;
using Common.Util;
using GameInterface.Extentions;
using GameInterface.Policies;
using GameInterface.Services.Settlements.Messages;
using HarmonyLib;
using Serilog;
using System;
using System.Collections.Generic;
using System.Text;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Library;

namespace GameInterface.Services.Settlements.Patches;

/// <summary>
/// Used to Patch Settlement.SetWallSectionHitPointsRatioAtIndex() server side sync.
/// </summary>
[HarmonyPatch(typeof(Settlement))]
internal class SetWallHitPointsSettlementPatch
{
    private static ILogger Logger = LogManager.GetLogger<Settlement>();

    [HarmonyPatch("SetWallSectionHitPointsRatioAtIndex")]
    [HarmonyPrefix]
    private static bool SetWallSectionHitPointsRatioAtIndexPrefix(ref Settlement __instance, ref int index, ref float hitPointsRatio)
    {
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;

        if (ModInformation.IsClient)
        {
            Logger.Error("Client created unmanaged {name}\n"
                + "Callstack: {callstack}", typeof(Settlement), Environment.StackTrace);
            return true;
        }

        var wallSectionHitPointsRatioList = __instance.SettlementWallSectionHitPointsRatioList;

        wallSectionHitPointsRatioList[index] = MBMath.ClampFloat(hitPointsRatio, 0f, 1f);

        MessageBroker.Instance.Publish(__instance, new SettlementWallHitPointsRatioChanged(__instance.StringId, index, hitPointsRatio));

        return true;
    }

    internal static void RunSetWallSectionHitPointsRatioAtIndex(Settlement settlement, int index, float hitPointsRatio)
    {
        GameLoopRunner.RunOnMainThread(() =>
        {
            using (new AllowedThread())
            {
                settlement.SettlementWallSectionHitPointsRatioList[index] = MBMath.ClampFloat(hitPointsRatio, 0f, 1f);
            }
        });
    }
}
