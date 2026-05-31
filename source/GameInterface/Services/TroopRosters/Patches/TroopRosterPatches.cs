using Common;
using Common.Logging;
using Common.Messaging;
using GameInterface.Policies;
using GameInterface.Services.TroopRosters.Messages;
using HarmonyLib;
using Serilog;
using System;
using System.Collections.Generic;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Roster;

namespace GameInterface.Services.TroopRosters.Patches;

[HarmonyPatch(typeof(TroopRoster))]
internal class TroopRosterPatches
{
    private static readonly ILogger Logger = LogManager.GetLogger<TroopRosterPatches>();

    [HarmonyPatch(nameof(TroopRoster.AddToCounts))]
    [HarmonyPrefix]
    private static void PrefixAddToCounts(ref TroopRoster __instance, CharacterObject character, int count, bool insertAtFront,
    int woundedCount, int xpChange, bool removeDepleted, int index)
    {
        if (CallOriginalPolicy.IsOriginalAllowed()) return;

        if (ModInformation.IsClient)
        {
            Logger.Error("Client attempted to {methodName} managed {type}", nameof(ItemRoster.AddToCounts), typeof(ItemRoster));
            return;
        }

        var message = new TroopRosterAddToCountsChanged(__instance, character, count, insertAtFront, woundedCount, xpChange, removeDepleted, index);

        MessageBroker.Instance.Publish(__instance, message);
    }

    [HarmonyPatch(nameof(TroopRoster.RemoveTroop))]
    [HarmonyPrefix]
    public static void PrefixRemoveTroop(TroopRoster __instance, CharacterObject troop, int numberToRemove, int xp)
    {
        if (CallOriginalPolicy.IsOriginalAllowed()) return;

        if (ModInformation.IsClient)
        {
            Logger.Error("Client attempted to RemoveTroop from a managed {type}", typeof(TroopRoster));
            return;
        }

        var message = new TroopRemoved(__instance, troop, numberToRemove, xp);
        MessageBroker.Instance.Publish(__instance, message);
    }

    [HarmonyPatch(nameof(TroopRoster.WoundTroop))]
    [HarmonyPrefix]
    public static void PrefixWoundTroop(TroopRoster __instance, CharacterObject troop, int numberToWound)
    {
        if (CallOriginalPolicy.IsOriginalAllowed()) return;

        if (ModInformation.IsClient)
        {
            Logger.Error("Client attempted to WoundTroop from a managed {type}", typeof(TroopRoster));
            return;
        }

        var message = new TroopRosterTroopWounded(__instance, troop, numberToWound);
        MessageBroker.Instance.Publish(__instance, message);
    }

    [HarmonyPatch(nameof(TroopRoster.RemoveZeroCounts))]
    [HarmonyPrefix]
    public static void PrefixRemoveZeroCounts(TroopRoster __instance)
    {
        if (CallOriginalPolicy.IsOriginalAllowed()) return;

        if (ModInformation.IsClient)
        {
            Logger.Error("Client attempted to RemoveZeroCounts from a managed {type}", typeof(TroopRoster));
            return;
        }

        var message = new ZeroCountsRemoved(__instance);
        MessageBroker.Instance.Publish(__instance, message);
    }

    [HarmonyPatch(nameof(TroopRoster.AddXpToTroopAtIndex))]
    [HarmonyPrefix]
    public static void PrefixAddXpToTroopAtIndex(TroopRoster __instance, int index, int xpAmount)
    {
        if (CallOriginalPolicy.IsOriginalAllowed()) return;

        if (ModInformation.IsClient)
        {
            Logger.Error("Client attempted to AddXpToTroopAtIndex from a managed {type}", typeof(TroopRoster));
            return;
        }

        var message = new XpAtTroopIndexAdded(__instance, index, xpAmount);
        MessageBroker.Instance.Publish(__instance, message);
    }

    [HarmonyPatch(nameof(TroopRoster.Clear))]
    [HarmonyPrefix]
    public static void PrefixClear(TroopRoster __instance)
    {
        if (CallOriginalPolicy.IsOriginalAllowed()) return;

        if (ModInformation.IsClient)
        {
            Logger.Error("Client attempted to Clear from a managed {type}", typeof(TroopRoster));
            return;
        }

        var message = new TroopRosterCleared(__instance);
        MessageBroker.Instance.Publish(__instance, message);
    }
}
