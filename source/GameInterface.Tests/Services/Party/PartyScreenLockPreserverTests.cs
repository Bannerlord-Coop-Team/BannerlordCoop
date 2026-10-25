using GameInterface.Services.Party;
using GameInterface.Tests.Services.LiveTesting;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.ViewModelCollection.Party;
using TaleWorlds.Library;
using TaleWorlds.ScreenSystem;
using Xunit;

namespace GameInterface.Tests.Services.Party;

// Shares the collection that owns ScreenManager.TopScreen in tests, because it is static.
[Collection(nameof(GauntletAutomationCollection))]
public class PartyScreenLockPreserverTests
{
    [Fact]
    public void ServerRefreshReset_KeepsLockTogglesWhileScreenIsOpen()
    {
        var tracker = new ViewDataTrackerCampaignBehavior();
        var logic = new PartyScreenLogic();
        var troop = CreateRow("troop", isPrisoner: false);
        var prisoner = CreateRow("prisoner", isPrisoner: true);
        var partyVm = CreatePartyVm(logic, tracker, troop, prisoner);

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

        using (new TopPartyScreen(partyVm))
        {
            new PartyScreenLockPreserver().Reset(logic, () => logic.OnReset(false));
        }

        Assert.Equal(1, resetCount);
        AssertLocksKept(partyVm, tracker, troop, prisoner);
    }

    [Fact]
    public void OpenScreenCommitReset_KeepsLiveLockLists()
    {
        var tracker = new ViewDataTrackerCampaignBehavior();
        var logic = new PartyScreenLogic();
        var troop = CreateRow("troop", isPrisoner: false);
        var prisoner = CreateRow("prisoner", isPrisoner: true);
        var partyVm = CreatePartyVm(logic, tracker, troop, prisoner);
        partyVm._mainPartySortController = new PartySortControllerVM(PartyScreenLogic.PartyRosterSide.Right, null);
        // The real vanilla handler: a cancel style reset puts back the open locks but leaves the rows alone.
        AddAfterReset(logic, partyVm.AfterReset);

        using (new TopPartyScreen(partyVm))
        {
            new PartyScreenLockPreserver().Reset(logic, () => logic.OnReset(true));
        }

        AssertLocksKept(partyVm, tracker, troop, prisoner);
    }

    [Fact]
    public void Reset_IgnoresScreenOfAnotherLogic()
    {
        var tracker = new ViewDataTrackerCampaignBehavior();
        var logic = new PartyScreenLogic();
        var otherLogic = new PartyScreenLogic();
        var troop = CreateRow("troop", isPrisoner: false);
        var prisoner = CreateRow("prisoner", isPrisoner: true);
        var partyVm = CreatePartyVm(otherLogic, tracker, troop, prisoner);
        int resetCount = 0;
        AddAfterReset(logic, (_, _) => resetCount++);

        using (new TopPartyScreen(partyVm))
        {
            new PartyScreenLockPreserver().Reset(logic, () => logic.OnReset(true));
        }

        Assert.Equal(1, resetCount);
        Assert.Empty(tracker.GetPartyTroopLocks());
        Assert.Empty(tracker.GetPartyPrisonerLocks());
    }

    private static void AssertLocksKept(
        PartyVM partyVm,
        ViewDataTrackerCampaignBehavior tracker,
        PartyCharacterVM troop,
        PartyCharacterVM prisoner)
    {
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

    // Nothing was locked when the screen opened; the player then locked one troop and one prisoner row.
    private static PartyVM CreatePartyVm(
        PartyScreenLogic logic,
        ViewDataTrackerCampaignBehavior tracker,
        PartyCharacterVM troop,
        PartyCharacterVM prisoner)
    {
        var partyVm = (PartyVM)FormatterServices.GetUninitializedObject(typeof(PartyVM));
        partyVm.PartyScreenLogic = logic;
        System.Runtime.CompilerServices.Unsafe.AsRef(in partyVm._viewDataTracker) = tracker;
        partyVm._initialLockedTroopIds = new List<string>();
        partyVm._initialLockedPrisonerIds = new List<string>();
        partyVm._lockedTroopIds = new List<string> { "troop" };
        partyVm._lockedPrisonerIds = new List<string> { "prisoner" };
        partyVm._mainPartyTroops = new MBBindingList<PartyCharacterVM> { troop };
        partyVm._mainPartyPrisoners = new MBBindingList<PartyCharacterVM> { prisoner };
        troop._isLocked = true;
        prisoner._isLocked = true;
        return partyVm;
    }

    private static PartyCharacterVM CreateRow(string stringId, bool isPrisoner)
    {
        var character = new CharacterObject { StringId = stringId };
        var row = (PartyCharacterVM)FormatterServices.GetUninitializedObject(typeof(PartyCharacterVM));
        row._troop = new TroopRosterElement(character);
        row._isPrisoner = isPrisoner;
        return row;
    }

    private sealed class TopPartyScreen : IDisposable
    {
        private readonly ScreenBase previousScreen = ScreenManager.TopScreen;

        public TopPartyScreen(PartyVM partyVm)
        {
            // GameInterface.Tests does not reference SandBox.GauntletUI, so the screen type is loaded by name.
            var screenType = Type.GetType("SandBox.GauntletUI.GauntletPartyScreen, SandBox.GauntletUI", throwOnError: true);
            var screen = (ScreenBase)FormatterServices.GetUninitializedObject(screenType);
            AccessTools.Field(screenType, "_dataSource").SetValue(screen, partyVm);
            ScreenManager.TopScreen = screen;
        }

        public void Dispose() => ScreenManager.TopScreen = previousScreen;
    }
}
