using Common;
using Common.Logging;
using Common.Messaging;
using Common.Util;
using GameInterface.Policies;
using GameInterface.Services.Settlements.Messages;
using HarmonyLib;
using Serilog;
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
            Logger.Error("Client created managed {name}", typeof(Settlement));
            return true;
        }

        var wallSectionHitPointsRatioList = __instance.SettlementWallSectionHitPointsRatioList;

        wallSectionHitPointsRatioList[index] = MBMath.ClampFloat(hitPointsRatio, 0f, 1f);

        MessageBroker.Instance.Publish(__instance, new SettlementWallHitPointsRatioChanged(__instance, index, hitPointsRatio));

        return true;
    }

    internal static void RunSetWallSectionHitPointsRatioAtIndex(Settlement settlement, int index, float hitPointsRatio)
    {
        GameThread.RunSafe(() =>
        {
            using (new AllowedThread())
            {
                var ratios = settlement.SettlementWallSectionHitPointsRatioList;
                bool wasBroken = ratios[index] <= 0f;
                float clamped = MBMath.ClampFloat(hitPointsRatio, 0f, 1f);
                ratios[index] = clamped;

                // A wall section only swaps between its solid and broken mesh when its ratio crosses 0
                // (RefreshWallState), so only a visual-dirty on that flip changes anything. Dirtying on every
                // ratio delta instead makes SettlementVisualManager destroy and re-create every siege engine
                // entity at its rest orientation each hit, which snaps aiming engines back and reads as jitter.
                if (wasBroken != (clamped <= 0f))
                {
                    settlement.Party?.SetVisualAsDirty();
                }
            }
        });
    }
}
