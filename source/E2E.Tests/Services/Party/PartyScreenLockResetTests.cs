using Common.Util;
using E2E.Tests.Environment;
using E2E.Tests.Environment.Instance;
using E2E.Tests.Util;
using GameInterface.Services.Heroes.Patches;
using HarmonyLib;
using Helpers;
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
using Xunit.Abstractions;

namespace E2E.Tests.Services.Party;

/// <summary>
/// After a conversation from the party screen, <c>GauntletPartyScreen.OnResume</c> commits the open screen
/// through <c>DoneLogic</c>, and Coop's commit runs <c>Reset(true)</c>. The player's lock toggles must survive
/// that reset, otherwise transfer all moves rows that still show as locked.
/// </summary>
public class PartyScreenLockResetTests : IDisposable
{
    // E2E.Tests does not reference TaleWorlds.ScreenSystem, so the screen types are loaded by name.
    private static readonly Type PartyScreenType =
        Type.GetType("SandBox.GauntletUI.GauntletPartyScreen, SandBox.GauntletUI", throwOnError: true);
    private static readonly PropertyInfo TopScreenProperty = AccessTools.Property(
        Type.GetType("TaleWorlds.ScreenSystem.ScreenManager, TaleWorlds.ScreenSystem", throwOnError: true), "TopScreen");

    private E2ETestEnvironment TestEnvironment { get; }
    private EnvironmentInstance Server => TestEnvironment.Server;
    private EnvironmentInstance Client => TestEnvironment.Clients.First();

    public PartyScreenLockResetTests(ITestOutputHelper output)
    {
        TestEnvironment = new E2ETestEnvironment(output);
        // Transfer all reads the upgrade and prisoner recruitment models.
        XpCapModels.Install(Client);
    }

    public void Dispose() => TestEnvironment.Dispose();

    [Fact]
    public void OpenScreenCommit_KeepsLiveLocks_SoTransferAllSkipsLockedRows()
    {
        string partyId = null;
        string lockedTroopId = null;
        string freeTroopId = null;
        string lockedPrisonerId = null;
        string freePrisonerId = null;

        Server.Call(() =>
        {
            var party = GameObjectCreator.CreateInitializedObject<MobileParty>();
            var lockedTroop = GameObjectCreator.CreateInitializedObject<CharacterObject>();
            var freeTroop = GameObjectCreator.CreateInitializedObject<CharacterObject>();
            var lockedPrisoner = GameObjectCreator.CreateInitializedObject<CharacterObject>();
            var freePrisoner = GameObjectCreator.CreateInitializedObject<CharacterObject>();
            party.MemberRoster.AddToCounts(lockedTroop, 5);
            party.MemberRoster.AddToCounts(freeTroop, 5);
            party.PrisonRoster.AddToCounts(lockedPrisoner, 2);
            party.PrisonRoster.AddToCounts(freePrisoner, 2);

            Assert.True(Server.ObjectManager.TryGetId(party, out partyId));
            Assert.True(Server.ObjectManager.TryGetId(lockedTroop, out lockedTroopId));
            Assert.True(Server.ObjectManager.TryGetId(freeTroop, out freeTroopId));
            Assert.True(Server.ObjectManager.TryGetId(lockedPrisoner, out lockedPrisonerId));
            Assert.True(Server.ObjectManager.TryGetId(freePrisoner, out freePrisonerId));
        });
        TestEnvironment.FlushCoalescer();

        Client.Call(() =>
        {
            Assert.True(Client.ObjectManager.TryGetObject<MobileParty>(partyId, out var party));
            Assert.True(Client.ObjectManager.TryGetObject<CharacterObject>(lockedTroopId, out var lockedTroop));
            Assert.True(Client.ObjectManager.TryGetObject<CharacterObject>(freeTroopId, out var freeTroop));
            Assert.True(Client.ObjectManager.TryGetObject<CharacterObject>(lockedPrisonerId, out var lockedPrisoner));
            Assert.True(Client.ObjectManager.TryGetObject<CharacterObject>(freePrisonerId, out var freePrisoner));
            using (new AllowedThread())
            {
                // Upgrade targets are not synced, and transfer reads them for every row it moves.
                foreach (var element in party.MemberRoster.GetTroopRoster().Concat(party.PrisonRoster.GetTroopRoster()))
                    element.Character.UpgradeTargets ??= Array.Empty<CharacterObject>();
            }

            using var mainHero = new MainHeroSubstitutionScope(party.LeaderHero, party);
            var tracker = new ViewDataTrackerCampaignBehavior();
            Campaign.Current.AddCampaignBehaviorManager(new CampaignBehaviorManager(new CampaignBehaviorBase[] { tracker }));
            // The party name in the init data builds an encyclopedia link.
            Campaign.Current.EncyclopediaManager ??= new TaleWorlds.CampaignSystem.Encyclopedia.EncyclopediaManager();
            Campaign.Current.EncyclopediaManager.CreateEncyclopediaPages();

            var logic = new PartyScreenLogic();
            logic.Initialize(PartyScreenLogicInitializationData.CreateBasicInitDataWithMainParty(
                TroopRoster.CreateDummyTroopRoster(), TroopRoster.CreateDummyTroopRoster(),
                PartyScreenLogic.TransferState.Transferable, PartyScreenLogic.TransferState.Transferable,
                PartyScreenLogic.TransferState.Transferable, null, PartyScreenHelper.PartyScreenMode.Normal,
                partyPresentationDoneButtonDelegate: (_, _, _, _, _, _, _, _, _) => true));

            // Nothing was locked when the screen opened; the player then locked one troop and one prisoner row.
            var lockedTroopRow = CreateRow(logic, lockedTroop, isPrisoner: false);
            var lockedPrisonerRow = CreateRow(logic, lockedPrisoner, isPrisoner: true);
            var partyVm = (PartyVM)FormatterServices.GetUninitializedObject(typeof(PartyVM));
            partyVm.PartyScreenLogic = logic;
            System.Runtime.CompilerServices.Unsafe.AsRef(in partyVm._viewDataTracker) = tracker;
            partyVm._initialLockedTroopIds = new List<string>();
            partyVm._initialLockedPrisonerIds = new List<string>();
            partyVm._lockedTroopIds = new List<string> { lockedTroop.StringId };
            partyVm._lockedPrisonerIds = new List<string> { lockedPrisoner.StringId };
            partyVm._mainPartyTroops = new MBBindingList<PartyCharacterVM> { lockedTroopRow };
            partyVm._mainPartyPrisoners = new MBBindingList<PartyCharacterVM> { lockedPrisonerRow };
            partyVm._mainPartySortController = new PartySortControllerVM(PartyScreenLogic.PartyRosterSide.Right, null);
            // The publicizer exposes both the event and its backing field, so add vanilla's handler by reflection.
            typeof(PartyScreenLogic).GetEvent("AfterReset")
                .AddEventHandler(logic, (PartyScreenLogic.AfterResetDelegate)partyVm.AfterReset);

            var screen = FormatterServices.GetUninitializedObject(PartyScreenType);
            AccessTools.Field(PartyScreenType, "_dataSource").SetValue(screen, partyVm);
            var previousScreen = TopScreenProperty.GetValue(null);
            TopScreenProperty.SetValue(null, screen);
            try
            {
                // What GauntletPartyScreen.OnResume does once the conversation ends.
                Assert.True(logic.DoneLogic(false));
            }
            finally
            {
                TopScreenProperty.SetValue(null, previousScreen);
            }

            Assert.Equal(new[] { lockedTroop.StringId }, partyVm._lockedTroopIds);
            Assert.Equal(new[] { lockedPrisoner.StringId }, partyVm._lockedPrisonerIds);
            Assert.True(lockedTroopRow.IsLocked);
            Assert.True(lockedPrisonerRow.IsLocked);

            using (new AllowedThread())
            {
                partyVm.TransferAllCharacters(PartyScreenLogic.PartyRosterSide.Right, PartyScreenLogic.TroopType.Member);
                partyVm.TransferAllCharacters(PartyScreenLogic.PartyRosterSide.Right, PartyScreenLogic.TroopType.Prisoner);
            }

            var right = PartyScreenLogic.PartyRosterSide.Right;
            var left = PartyScreenLogic.PartyRosterSide.Left;
            Assert.Equal(5, logic.MemberRosters[(int)right].GetTroopCount(lockedTroop));
            Assert.Equal(0, logic.MemberRosters[(int)right].GetTroopCount(freeTroop));
            Assert.Equal(5, logic.MemberRosters[(int)left].GetTroopCount(freeTroop));
            Assert.Equal(2, logic.PrisonerRosters[(int)right].GetTroopCount(lockedPrisoner));
            Assert.Equal(0, logic.PrisonerRosters[(int)right].GetTroopCount(freePrisoner));
            Assert.Equal(2, logic.PrisonerRosters[(int)left].GetTroopCount(freePrisoner));
        });
    }

    private static PartyCharacterVM CreateRow(PartyScreenLogic logic, CharacterObject character, bool isPrisoner)
    {
        var roster = isPrisoner
            ? logic.PrisonerRosters[(int)PartyScreenLogic.PartyRosterSide.Right]
            : logic.MemberRosters[(int)PartyScreenLogic.PartyRosterSide.Right];
        var row = (PartyCharacterVM)FormatterServices.GetUninitializedObject(typeof(PartyCharacterVM));
        row._troop = roster.GetElementCopyAtIndex(roster.FindIndexOfTroop(character));
        row._isPrisoner = isPrisoner;
        row._isLocked = true;
        return row;
    }
}
