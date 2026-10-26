using SandBox.GauntletUI;
using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.ViewModelCollection.Party;
using TaleWorlds.Library;
using TaleWorlds.ScreenSystem;

namespace GameInterface.Services.Party;

/// <summary>
/// Runs a Coop party screen reset without losing the player's troop and prisoner lock toggles.
/// </summary>
internal interface IPartyScreenLockPreserver
{
    void Reset(PartyScreenLogic logic, Action reset);
}

/// <summary>
/// Vanilla PartyVM.AfterReset restores the troop locks from screen open and saves them,
/// so Coop's extra resets would drop the player's lock toggles without this.
/// </summary>
internal class PartyScreenLockPreserver : IPartyScreenLockPreserver
{
    // PartyVM is the only AfterReset handler, so a reset without its screen has no locks to drop.
    // Transfer all saves the live vm lists to the tracker first, so those lists are the ones to keep.
    public void Reset(PartyScreenLogic logic, Action reset)
    {
        var partyVm = FindPartyVm(logic);
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
        RefreshRows(partyVm, partyVm.MainPartyTroops);
        RefreshRows(partyVm, partyVm.MainPartyPrisoners);
        partyVm.SaveCharacterLockStates();
    }

    private static PartyVM FindPartyVm(PartyScreenLogic logic)
    {
        var partyVm = (ScreenManager.TopScreen as GauntletPartyScreen)?._dataSource;
        return partyVm != null && ReferenceEquals(partyVm.PartyScreenLogic, logic)
            ? partyVm
            : null;
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
