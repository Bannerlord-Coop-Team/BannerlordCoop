using Common;
using Common.Logging;
using Common.Messaging;
using Common.Util;
using GameInterface.Policies;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.TroopRosters.Messages;
using HarmonyLib;
using Serilog;
using System;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Roster;

namespace GameInterface.Services.TroopRosters.Patches;

/// <summary>
/// Patches the TroopRoster mutators on the authority so each change is published as an event keyed by the
/// element's <see cref="CharacterObject"/>, resolved while the index is still valid. TroopRosterDeltaHandler
/// turns these into per-operation identity-keyed network messages.
/// </summary>
[HarmonyPatch(typeof(TroopRoster))]
internal class TroopRosterPatches
{
    private static readonly ILogger Logger = LogManager.GetLogger<TroopRosterPatches>();

    // Set while a TroopRoster.AddToCountsAtIndex call is running. AddToCountsAtIndex sets xp through the
    // patched SetElementXp internally (only when xpChange != 0), which would publish a redundant
    // ElementXpSet on top of the CountsAtIndexAdded that already carries the xp change. The flag lets the
    // SetElementXp patch skip that nested publish while still firing for direct SetElementXp callers.
    [ThreadStatic]
    private static bool _inAddToCountsAtIndex;

    /// <summary>
    /// True when the registry can key a network message on this roster. A battle mutates the MapEventParty
    /// tally rosters (died/wounded/routed) once per casualty; those are dummy rosters with no OwnerParty,
    /// never registered, with nothing to replicate. Without this gate each of those thousands of mutations
    /// would allocate an event and dispatch it synchronously on the game thread only for the send handler to
    /// drop it on the missing id. Real party rosters register on set_OwnerParty before their first mutation
    /// (<see cref="TroopRosterOwnerPartyRegistrationPatch"/>), so this only skips rosters that have no
    /// identity to send anyway.
    /// </summary>
    internal static bool IsRegistered(TroopRoster roster)
        => ContainerProvider.TryResolve<IObjectManager>(out var objectManager) && objectManager.TryGetId(roster, out _);

    [HarmonyPatch(nameof(TroopRoster.AddToCountsAtIndex))]
    [HarmonyPrefix]
    private static void PrefixAddToCountsAtIndex(TroopRoster __instance, int index, int countChange,
        int woundedCountChange, int xpChange, bool removeDepleted)
    {
        if (CallOriginalPolicy.IsOriginalAllowed()) return;
        if (ModInformation.IsClient)
        {
            Logger.Error("Client attempted to {methodName} on a managed {type}", nameof(TroopRoster.AddToCountsAtIndex), typeof(TroopRoster));
            return;
        }

        // Skip rosters with no network identity (battle tally dummies, etc.) before allocating/publishing.
        if (!IsRegistered(__instance)) return;

        // Match the bounds guard the SetElement* prefixes use: an index in the slot-padding window
        // (Count <= index < data.Length) would read a cleared element with a null Character.
        if (index < 0 || index >= __instance.Count) return;

        // Resolve the element by identity now: the index is valid here (pre-mutation), but a subtract-to-zero
        // with removeDepleted removes it before a postfix could read it back.
        var character = __instance.GetElementCopyAtIndex(index).Character;
        MessageBroker.Instance.Publish(__instance,
            new CountsAtIndexAdded(__instance, character, countChange, woundedCountChange, xpChange, removeDepleted));

        _inAddToCountsAtIndex = true;
    }

    [HarmonyPatch(nameof(TroopRoster.AddToCountsAtIndex))]
    [HarmonyFinalizer]
    private static void FinalizerAddToCountsAtIndex()
    {
        // A finalizer (not a postfix) so the flag clears even when the original throws. A skipped clear would
        // leave it stuck true on this thread and silence every later direct SetElementXp publish.
        _inAddToCountsAtIndex = false;
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

        if (!IsRegistered(__instance)) return;

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

        if (!IsRegistered(__instance)) return;

        // Only read the element while the index points at a live slot. data has padding past Count, so an
        // out-of-range index would publish a change for a stale/null element before the original throws.
        if (index < 0 || index >= __instance.Count) return;

        var character = __instance.GetElementCopyAtIndex(index).Character;
        MessageBroker.Instance.Publish(__instance, new ElementNumberSet(__instance, character, number));
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

        if (!IsRegistered(__instance)) return;

        if (index < 0 || index >= __instance.Count) return;

        var character = __instance.GetElementCopyAtIndex(index).Character;
        MessageBroker.Instance.Publish(__instance, new ElementWoundedNumberSet(__instance, character, number));
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

        if (!IsRegistered(__instance)) return;

        // The xp change AddToCountsAtIndex makes internally is already carried by its CountsAtIndexAdded.
        if (_inAddToCountsAtIndex) return;

        if (index < 0 || index >= __instance.Count) return;

        var character = __instance.GetElementCopyAtIndex(index).Character;
        MessageBroker.Instance.Publish(__instance, new ElementXpSet(__instance, character, number));
    }

    // ─── TEMP [RosterDiag] ──────────────────────────────────────────────────────────────────────────────────
    // Server-side trace to nail the captured-party "roster goes to -1" bug. Fires only for REGISTERED (real
    // party) rosters whose total has landed at or below zero, and dumps the CALLER STACK so we can see exactly
    // which operation drove it there (a battle casualty's OnTroopKilled, the capture's CaptureDefeatedPartyMembers,
    // a loot step, etc.) and whether the same troop is removed twice. Quiet outside the failure window. REMOVE
    // once the root cause is confirmed.
    private static void DiagRoster(TroopRoster roster, string op)
    {
        if (ModInformation.IsClient || roster == null) return;
        if (roster.TotalManCount > 0) return; // only the failure zone: a real party at 0 or negative
        if (!ContainerProvider.TryResolve<IObjectManager>(out var objectManager) || !objectManager.TryGetId(roster, out var rosterId)) return;

        var frames = Environment.StackTrace.Split('\n');
        var caller = string.Join("\n", frames.Skip(3).Take(18));
        Logger.Warning("[RosterDiag] {Op} -> roster={Roster} Total={Total} Count={Count}\n{Caller}",
            op, rosterId, roster.TotalManCount, roster.Count, caller);
    }

    [HarmonyPatch(nameof(TroopRoster.AddToCountsAtIndex))]
    [HarmonyPostfix]
    private static void DiagPostfix_AddToCountsAtIndex(TroopRoster __instance, int countChange, int woundedCountChange, bool removeDepleted)
        => DiagRoster(__instance, $"AddToCountsAtIndex(n={countChange}, w={woundedCountChange}, removeDepleted={removeDepleted})");

    [HarmonyPatch(nameof(TroopRoster.SetElementNumber))]
    [HarmonyPostfix]
    private static void DiagPostfix_SetElementNumber(TroopRoster __instance, int number)
        => DiagRoster(__instance, $"SetElementNumber({number})");

    [HarmonyPatch(nameof(TroopRoster.SetElementWoundedNumber))]
    [HarmonyPostfix]
    private static void DiagPostfix_SetElementWoundedNumber(TroopRoster __instance, int number)
        => DiagRoster(__instance, $"SetElementWoundedNumber({number})");
}

// Some co-op flows commit troops via AddToCounts while already inside an AllowedThread opened for another
// reason, prisoner/battle capture for example. Inside an AllowedThread the lower-level AddToCountsAtIndex
// patch stands down, so this patch publishes the change for that case using the character parameter, which
// stays valid even when removeDepleted has removed the element.
[HarmonyPatch(typeof(TroopRoster), nameof(TroopRoster.AddToCounts))]
internal class TroopRosterAddToCountsPatch
{
    [HarmonyPostfix]
    static void Postfix(TroopRoster __instance, CharacterObject character, int count,
        int woundedCount, int xpChange, bool removeDepleted)
    {
        if (!AllowedThread.IsThisThreadAllowed()) return;
        if (ModInformation.IsClient) return;
        if (__instance == null || character == null) return;
        if (!TroopRosterPatches.IsRegistered(__instance)) return;

        MessageBroker.Instance.Publish(__instance, new CountsAtIndexAdded(__instance, character, count, woundedCount, xpChange, removeDepleted));
    }
}
