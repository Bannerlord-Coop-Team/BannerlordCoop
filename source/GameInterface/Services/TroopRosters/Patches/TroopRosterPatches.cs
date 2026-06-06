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
/// <summary>
/// Explicitly publishes troop roster sync message when recruitment happens inside an AllowedThread context.
/// Normally <see cref="TroopRoster.AddToCounts"/> patch handles sync, but when
/// <see cref="EnterSettlementAction.ApplyForParty"/> runs inside AllowedThread (via OverrideApplyForParty),
/// the AddToCounts patch is suppressed by CallOriginalPolicy. This patch fires before ApplyInternal
/// and explicitly sends the sync message to clients.
/// </summary>
[HarmonyPatch(typeof(RecruitmentCampaignBehavior), "ApplyInternal")]
internal class RecruitmentCampaignBehaviorPatch
{
    private static readonly ILogger Logger = LogManager.GetLogger<RecruitmentCampaignBehaviorPatch>();

    [HarmonyPrefix]
    static void PrefixApplyInternal(MobileParty side1Party, CharacterObject troop, int number,
        RecruitmentCampaignBehavior.RecruitingDetail detail)
    {
        if (!AllowedThread.IsThisThreadAllowed()) return;
        if (ModInformation.IsClient) return;
        if (side1Party?.MemberRoster == null) return;

        var actualNumber = (detail == RecruitmentCampaignBehavior.RecruitingDetail.VolunteerFromIndividual ||
                            detail == RecruitmentCampaignBehavior.RecruitingDetail.VolunteerFromIndividualToGarrison)
                            ? 1 : number;

        var message = new TroopRosterAddToCountsChanged(side1Party.MemberRoster, troop, actualNumber, false, 0, 0, true, -1);

        MessageBroker.Instance.Publish(side1Party.MemberRoster, message);
    }
}
