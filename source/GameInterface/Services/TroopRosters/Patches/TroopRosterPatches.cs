using Common;
using Common.Logging;
using Common.Messaging;
using Common.Util;
using GameInterface.Policies;
using GameInterface.Services.TroopRosters.Messages;
using HarmonyLib;
using Serilog;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;

namespace GameInterface.Services.TroopRosters.Patches;

/// <summary>
/// Patches required for the TroopRoster
/// </summary>
[HarmonyPatch(typeof(TroopRoster))]
internal class TroopRosterPatches
{
    private static readonly ILogger Logger = LogManager.GetLogger<TroopRosterPatches>();

    [HarmonyPatch(nameof(TroopRoster.AddToCountsAtIndex))]
    [HarmonyPrefix]
    private static void PrefixAddToCountsAtIndex(TroopRoster __instance, int index, int countChange,
        int woundedCountChange, int xpChange, bool removeDepleted, ref bool __state)
    {
        if (CallOriginalPolicy.IsOriginalAllowed()) return;
        if (ModInformation.IsClient)
        {
            Logger.Error("Client attempted to {methodName} on a managed {type}", nameof(TroopRoster.AddToCountsAtIndex), typeof(TroopRoster));
            return;
        }

        MessageBroker.Instance.Publish(__instance,
            new CountsAtIndexAdded(__instance, index, countChange, woundedCountChange, xpChange, removeDepleted));
    }

    [HarmonyPatch(nameof(TroopRoster.AddNewElement))]
    [HarmonyPrefix]
    private static void PrefixAddNewElement(TroopRoster __instance, CharacterObject character, int insertionIndex)
    {
        if (CallOriginalPolicy.IsOriginalAllowed()) return;

        if (ModInformation.IsClient)
        {
            Logger.Error("Client attempted to {methodName} on a managed {type}", nameof(TroopRoster.AddNewElement), typeof(TroopRoster));
            return;
        }

        MessageBroker.Instance.Publish(__instance, new NewElementAdded(__instance, character, insertionIndex));
    }

    [HarmonyPatch(nameof(TroopRoster.RemoveZeroCounts))]
    [HarmonyPrefix]
    private static void PrefixRemoveZeroCounts(TroopRoster __instance)
    {
        if (CallOriginalPolicy.IsOriginalAllowed()) return;

        if (ModInformation.IsClient)
        {
            Logger.Error("Client attempted to {methodName} on a managed {type}", nameof(TroopRoster.RemoveZeroCounts), typeof(TroopRoster));
            return;
        }

        MessageBroker.Instance.Publish(__instance, new ZeroCountsRemoved(__instance));
    }

    [HarmonyPatch(nameof(TroopRoster.SetElementNumber))]
    [HarmonyPrefix]
    private static void PrefixSetElementNumber(TroopRoster __instance, int index, int number)
    {
        if (CallOriginalPolicy.IsOriginalAllowed()) return;

        if (ModInformation.IsClient)
        {
            Logger.Error("Client attempted to {methodName} on a managed {type}", nameof(TroopRoster.SetElementNumber), typeof(TroopRoster));
            return;
        }

        MessageBroker.Instance.Publish(__instance, new ElementNumberSet(__instance, index, number));
    }

    [HarmonyPatch(nameof(TroopRoster.SetElementWoundedNumber))]
    [HarmonyPrefix]
    private static void PrefixSetElementWoundedNumber(TroopRoster __instance, int index, int number)
    {
        if (CallOriginalPolicy.IsOriginalAllowed()) return;

        if (ModInformation.IsClient)
        {
            Logger.Error("Client attempted to {methodName} on a managed {type}", nameof(TroopRoster.SetElementWoundedNumber), typeof(TroopRoster));
            return;
        }

        MessageBroker.Instance.Publish(__instance, new ElementWoundedNumberSet(__instance, index, number));
    }

    [HarmonyPatch(nameof(TroopRoster.SetElementXp))]
    [HarmonyPrefix]
    private static void PrefixSetElementXp(TroopRoster __instance, int index, int number)
    {
        if (CallOriginalPolicy.IsOriginalAllowed()) return;

        if (ModInformation.IsClient)
        {
            Logger.Error("Client attempted to {methodName} on a managed {type}", nameof(TroopRoster.SetElementXp), typeof(TroopRoster));
            return;
        }

        MessageBroker.Instance.Publish(__instance, new ElementXpSet(__instance, index, number));
    }

    [HarmonyPatch(nameof(TroopRoster.ShiftTroopToIndex))]
    [HarmonyPrefix]
    private static void PrefixShiftTroopToIndex(TroopRoster __instance, int troopIndex, int targetIndex)
    {
        if (CallOriginalPolicy.IsOriginalAllowed()) return;

        if (ModInformation.IsClient)
        {
            Logger.Error("Client attempted to {methodName} on a managed {type}", nameof(TroopRoster.ShiftTroopToIndex), typeof(TroopRoster));
            return;
        }

        MessageBroker.Instance.Publish(__instance, new TroopShiftedToIndex(__instance, troopIndex, targetIndex));
    }

    [HarmonyPatch(nameof(TroopRoster.SwapTroopsAtIndices))]
    [HarmonyPrefix]
    private static void PrefixSwapTroopsAtIndices(TroopRoster __instance, int firstIndex, int secondIndex)
    {
        if (CallOriginalPolicy.IsOriginalAllowed()) return;

        if (ModInformation.IsClient)
        {
            Logger.Error("Client attempted to {methodName} on a managed {type}", nameof(TroopRoster.SwapTroopsAtIndices), typeof(TroopRoster));
            return;
        }

        MessageBroker.Instance.Publish(__instance, new TroopsSwappedAtIndices(__instance, firstIndex, secondIndex));
    }
}
// RecruitmentCampaignBehavior.ApplyInternal runs inside AllowedThread (via OverrideApplyForParty),
// which suppresses the AddNewElement and AddToCountsAtIndex patches. This patch explicitly
// publishes sync messages for that case using the return value as the final index.
[HarmonyPatch(typeof(TroopRoster), nameof(TroopRoster.AddToCounts))]
internal class TroopRosterAddToCountsPatch
{
    [HarmonyPrefix]
    static void Prefix(TroopRoster __instance, CharacterObject character, ref bool __state)
    {
        if (!AllowedThread.IsThisThreadAllowed()) return;
        if (ModInformation.IsClient) return;
        if (__instance == null || character == null) return;

        // track if this is a new element
        __state = __instance.FindIndexOfTroop(character) < 0;
    }

    [HarmonyPostfix]
    static void Postfix(TroopRoster __instance, CharacterObject character, int count,
        bool insertAtFront, int woundedCount, int xpChange, bool removeDepleted, int __result, bool __state)
    {
        if (!AllowedThread.IsThisThreadAllowed()) return;
        if (ModInformation.IsClient) return;
        if (__instance == null || character == null) return;

        if (__state) // was new element
        {
            MessageBroker.Instance.Publish(__instance, new NewElementAdded(__instance, character, insertAtFront ? 0 : -1));
        }

        MessageBroker.Instance.Publish(__instance, new CountsAtIndexAdded(__instance, __result, count, woundedCount, xpChange, removeDepleted));
    }
}
