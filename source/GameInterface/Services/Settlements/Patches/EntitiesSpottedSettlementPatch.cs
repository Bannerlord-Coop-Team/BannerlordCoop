using Common;
using Common.Messaging;
using Common.Util;
using GameInterface.Policies;
using GameInterface.Services.Settlements.Messages;
using HarmonyLib;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.Settlements.Patches;


/// <summary>
/// Used to sync number of enemies and allies spotted around.
/// </summary>
[HarmonyPatch(typeof(Settlement))]
internal class EntitiesSpottedSettlementPatch
{
    [HarmonyPatch(nameof(Settlement.NumberOfEnemiesSpottedAround), MethodType.Setter)]
    [HarmonyPrefix]
    private static bool NumberEnemiesSpottedPrefix(ref Settlement __instance, ref float value)
    {
        if (AllowedThread.IsThisThreadAllowed()) return true;
        if (PolicyProvider.AllowOriginalCalls) return true;

        if (ModInformation.IsClient) return false;

        // pub
        if (__instance.NumberOfEnemiesSpottedAround == value) return false;

        var message = new SettlementChangedEnemiesSpotted(__instance.StringId, value);
        MessageBroker.Instance.Publish(__instance, message);

        return true;
    }

    internal static void RunNumberOfEnemiesSpottedChange(Settlement settlement, float numberOfEnemiesSpottedAround)
    {
        GameLoopRunner.RunOnMainThread(() =>
        {
            using (new AllowedThread())
            {
                settlement.NumberOfEnemiesSpottedAround = numberOfEnemiesSpottedAround;
            }
        });
    }
}
