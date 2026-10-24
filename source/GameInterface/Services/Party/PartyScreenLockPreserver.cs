using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.ViewModelCollection.Party;
using TaleWorlds.Library;

namespace GameInterface.Services.Party;

/// <summary>
/// Vanilla PartyVM.AfterReset restores the troop locks from screen open and saves them,
/// so Coop's extra resets would drop the player's lock toggles without this.
/// </summary>
internal static class PartyScreenLockPreserver
{
    public static void Reset(IViewDataTracker tracker, Action reset)
    {
        var troopLocks = tracker.GetPartyTroopLocks().ToList();
        var prisonerLocks = tracker.GetPartyPrisonerLocks().ToList();

        reset();

        tracker.SetPartyTroopLocks(troopLocks);
        tracker.SetPartyPrisonerLocks(prisonerLocks);
    }

    public static void Reset(PartyVM partyVm, Action reset)
    {
        if (partyVm == null)
        {
            reset();
            return;
        }

        var troopLocks = partyVm._lockedTroopIds.ToList();
        var prisonerLocks = partyVm._lockedPrisonerIds.ToList();

        reset();

        Restore(partyVm._lockedTroopIds, troopLocks);
        Restore(partyVm._lockedPrisonerIds, prisonerLocks);
        partyVm.SaveCharacterLockStates();
        RefreshRows(partyVm, partyVm.MainPartyTroops);
        RefreshRows(partyVm, partyVm.MainPartyPrisoners);
    }

    private static void Restore(List<string> locks, List<string> snapshot)
    {
        locks.Clear();
        locks.AddRange(snapshot);
    }

    private static void RefreshRows(PartyVM partyVm, MBBindingList<PartyCharacterVM> rows)
    {
        foreach (var row in rows)
        {
            row.IsLocked = partyVm.IsTroopLocked(row.Troop, row.IsPrisoner);
        }
    }
}
