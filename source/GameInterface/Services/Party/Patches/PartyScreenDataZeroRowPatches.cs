using Common;
using HarmonyLib;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.Core;

namespace GameInterface.Services.Party.Patches;

[HarmonyPatch(typeof(PartyScreenData))]
internal static class PartyScreenDataZeroRowPatches
{
    [HarmonyPatch(nameof(PartyScreenData.ResetUsing))]
    [HarmonyPrefix]
    private static void ResetUsingPrefix(
        PartyScreenData partyScreenData,
        out PartyScreenZeroRows __state)
    {
        __state = ModInformation.IsClient
            ? PartyScreenZeroRows.Capture(partyScreenData)
            : null;
    }

    [HarmonyPatch(nameof(PartyScreenData.ResetUsing))]
    [HarmonyPostfix]
    private static void ResetUsingPostfix(
        PartyScreenData __instance,
        PartyScreenZeroRows __state)
    {
        __state?.Restore(__instance);
    }

    [HarmonyPatch(nameof(PartyScreenData.CopyFromScreenData))]
    [HarmonyPrefix]
    private static void CopyFromScreenDataPrefix(
        PartyScreenData data,
        out PartyScreenZeroRows __state)
    {
        __state = ModInformation.IsClient
            ? PartyScreenZeroRows.Capture(data)
            : null;
    }

    [HarmonyPatch(nameof(PartyScreenData.CopyFromScreenData))]
    [HarmonyPostfix]
    private static void CopyFromScreenDataPostfix(
        PartyScreenData __instance,
        PartyScreenZeroRows __state)
    {
        __state?.Restore(__instance);
    }
}

internal sealed class PartyScreenZeroRows
{
    private readonly List<PartyScreenZeroRow> rows = new List<PartyScreenZeroRow>();

    public static PartyScreenZeroRows Capture(PartyScreenData data)
    {
        var snapshot = new PartyScreenZeroRows();
        if (data == null) return snapshot;

        snapshot.Capture(data.LeftMemberRoster, PartyScreenRosterSlot.LeftMember);
        snapshot.Capture(data.LeftPrisonerRoster, PartyScreenRosterSlot.LeftPrisoner);
        snapshot.Capture(data.RightMemberRoster, PartyScreenRosterSlot.RightMember);
        snapshot.Capture(data.RightPrisonerRoster, PartyScreenRosterSlot.RightPrisoner);
        return snapshot;
    }

    public void Restore(PartyScreenData data)
    {
        if (data == null) return;

        foreach (var row in rows)
        {
            var roster = GetRoster(data, row.Slot);
            if (roster == null) continue;

            RosterElementState.Write(roster, row.Character, row.State);
        }
    }

    private void Capture(TroopRoster roster, PartyScreenRosterSlot slot)
    {
        if (roster == null) return;

        for (int index = 0; index < roster.Count; index++)
        {
            var element = roster.GetElementCopyAtIndex(index);
            if (element.Number != 0) continue;

            var state = RosterElementState.Read(roster, element.Character);
            if (!state.IsValid) continue;

            rows.Add(new PartyScreenZeroRow(slot, element.Character, state));
        }
    }

    private static TroopRoster GetRoster(
        PartyScreenData data,
        PartyScreenRosterSlot slot)
    {
        switch (slot)
        {
            case PartyScreenRosterSlot.LeftMember:
                return data.LeftMemberRoster;
            case PartyScreenRosterSlot.LeftPrisoner:
                return data.LeftPrisonerRoster;
            case PartyScreenRosterSlot.RightMember:
                return data.RightMemberRoster;
            case PartyScreenRosterSlot.RightPrisoner:
                return data.RightPrisonerRoster;
            default:
                return null;
        }
    }

    private readonly struct PartyScreenZeroRow
    {
        public readonly PartyScreenRosterSlot Slot;
        public readonly CharacterObject Character;
        public readonly RosterElementState State;

        public PartyScreenZeroRow(
            PartyScreenRosterSlot slot,
            CharacterObject character,
            RosterElementState state)
        {
            Slot = slot;
            Character = character;
            State = state;
        }
    }

    private enum PartyScreenRosterSlot
    {
        LeftMember,
        LeftPrisoner,
        RightMember,
        RightPrisoner,
    }
}
