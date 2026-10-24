using GameInterface.Services.Party;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.ViewModelCollection.Party;
using TaleWorlds.Library;
using Xunit;

namespace GameInterface.Tests.Services.Party;

public class PartyScreenLockPreserverTests
{
    [Fact]
    public void DoneReset_KeepsLocksSavedOnClose()
    {
        var tracker = new ViewDataTrackerCampaignBehavior();
        tracker.SetPartyTroopLocks(new[] { "initial_troop" });
        tracker.SetPartyPrisonerLocks(new[] { "initial_prisoner" });
        var initialTroops = tracker.GetPartyTroopLocks().ToList();
        var initialPrisoners = tracker.GetPartyPrisonerLocks().ToList();

        var logic = new PartyScreenLogic();
        int resetCount = 0;
        // Mimics vanilla PartyVM.AfterReset saving the locks from screen open.
        AddAfterReset(logic, (_, _) =>
        {
            resetCount++;
            tracker.SetPartyTroopLocks(initialTroops);
            tracker.SetPartyPrisonerLocks(initialPrisoners);
        });

        tracker.SetPartyTroopLocks(new[] { "new_troop" });
        tracker.SetPartyPrisonerLocks(Array.Empty<string>());

        PartyScreenLockPreserver.Reset(tracker, () => logic.OnReset(true));

        Assert.Equal(1, resetCount);
        Assert.Equal(new[] { "new_troop" }, tracker.GetPartyTroopLocks());
        Assert.Empty(tracker.GetPartyPrisonerLocks());
    }

    [Fact]
    public void ServerRefreshReset_KeepsLockTogglesWhileScreenIsOpen()
    {
        var tracker = new ViewDataTrackerCampaignBehavior();
        var troop = CreateRow("troop", isPrisoner: false);
        var prisoner = CreateRow("prisoner", isPrisoner: true);
        var partyVm = (PartyVM)FormatterServices.GetUninitializedObject(typeof(PartyVM));
        typeof(PartyVM).GetField("_viewDataTracker", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(partyVm, tracker);
        partyVm._initialLockedTroopIds = new List<string>();
        partyVm._initialLockedPrisonerIds = new List<string>();
        partyVm._lockedTroopIds = new List<string> { "troop" };
        partyVm._lockedPrisonerIds = new List<string> { "prisoner" };
        partyVm._mainPartyTroops = new MBBindingList<PartyCharacterVM> { troop };
        partyVm._mainPartyPrisoners = new MBBindingList<PartyCharacterVM> { prisoner };

        var logic = new PartyScreenLogic();
        int resetCount = 0;
        // Mimics vanilla PartyVM.AfterReset restoring the open locks and rebuilding rows.
        AddAfterReset(logic, (_, _) =>
        {
            resetCount++;
            partyVm._lockedTroopIds.Clear();
            partyVm._lockedTroopIds.AddRange(partyVm._initialLockedTroopIds);
            partyVm._lockedPrisonerIds.Clear();
            partyVm._lockedPrisonerIds.AddRange(partyVm._initialLockedPrisonerIds);
            partyVm.SaveCharacterLockStates();
            troop._isLocked = false;
            prisoner._isLocked = false;
        });

        PartyScreenLockPreserver.Reset(partyVm, () => logic.OnReset(false));

        Assert.Equal(1, resetCount);
        Assert.Equal(new[] { "troop" }, partyVm._lockedTroopIds);
        Assert.Equal(new[] { "prisoner" }, partyVm._lockedPrisonerIds);
        Assert.Equal(new[] { "troop" }, tracker.GetPartyTroopLocks());
        Assert.Equal(new[] { "prisoner" }, tracker.GetPartyPrisonerLocks());
        Assert.True(troop.IsLocked);
        Assert.True(prisoner.IsLocked);
    }

    // The publicizer exposes both the event and its backing field, so add the handler by reflection.
    private static void AddAfterReset(PartyScreenLogic logic, PartyScreenLogic.AfterResetDelegate handler)
        => typeof(PartyScreenLogic).GetEvent("AfterReset").AddEventHandler(logic, handler);

    private static PartyCharacterVM CreateRow(string stringId, bool isPrisoner)
    {
        var character = new CharacterObject { StringId = stringId };
        var row = (PartyCharacterVM)FormatterServices.GetUninitializedObject(typeof(PartyCharacterVM));
        row._troop = new TroopRosterElement(character);
        row._isPrisoner = isPrisoner;
        return row;
    }
}
