using Common.Messaging;
using GameInterface.Policies;
using GameInterface.Services.HeroDevelopers.Messages;
using HarmonyLib;
using System;
using TaleWorlds.CampaignSystem.CharacterDevelopment;

namespace GameInterface.Services.HeroDevelopers.Patches;

/// <summary>
/// Sends the mutations from one <see cref="HeroDeveloper.AddSkillXp"/> call as one ordered network batch.
/// </summary>
[HarmonyPatch(typeof(HeroDeveloper), nameof(HeroDeveloper.AddSkillXp))]
internal static class AddSkillXpBatchPatch
{
    [HarmonyPrefix]
    private static void Prefix(HeroDeveloper __instance, out HeroDeveloperBatchScope __state)
    {
        __state = CallOriginalPolicy.IsOriginalAllowed()
            ? null
            : HeroDeveloperBatchScope.Begin(__instance);
    }

    [HarmonyFinalizer]
    internal static Exception Finalizer(
        HeroDeveloper __instance,
        HeroDeveloperBatchScope __state,
        Exception __exception)
    {
        if (__state == null) return __exception;

        // The inner patches published synchronously before batching, even when AddSkillXp later threw.
        // Complete also restores a nested ambient scope before the original exception propagates.
        HeroDeveloperBatch batch = __state.Complete();
        if (batch != null)
        {
            MessageBroker.Instance.Publish(__instance, batch);
        }

        return __exception;
    }
}
