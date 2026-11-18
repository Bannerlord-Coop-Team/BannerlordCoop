using Common.Util;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Party;
using GameInterface.Services.TroopRosters.Interfaces;
using GameInterface.Services.TroopRosters.Logging;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameState;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.Core;
using Xunit;

namespace GameInterface.Tests.Services.Party;

public class PartyScreenRosterRefresherTests
{
    [Fact]
    public void ServerUpdate_RebasesCompatiblePendingEdit()
    {
        var character = new CharacterObject();
        var logic = CreateLogic(character, 5, 1, out var visible);
        visible.AddToCounts(character, -2);
        int notificationCount = 0;
        int applyCount = 0;

        var applied = CreateRefresher().TryApply(
            logic,
            visible,
            character,
            (roster, troop) =>
            {
                applyCount++;
                int index = roster.FindIndexOfTroop(troop);
                roster.SetElementWoundedNumber(index, 0);
                roster.InitializeCachedData();
            },
            () => notificationCount++);

        Assert.True(applied);
        Assert.Equal(1, applyCount);
        Assert.Equal(0, notificationCount);
        Assert.True(logic.IsThereAnyChanges());
        AssertRoster(logic.CurrentData.RightMemberRoster, character, 3, 0);
        AssertRoster(logic._initialData.RightMemberRoster, character, 5, 0);
    }

    [Fact]
    public void ServerUpdate_ResetsAllPendingEditsWhenRebaseIsInvalid()
    {
        var character = new CharacterObject();
        var logic = CreateLogic(character, 5, 1, out var visible);
        visible.AddToCounts(character, -4);
        logic.CurrentData.LeftMemberRoster.AddToCounts(character, 4);
        int notificationCount = 0;
        var lockPreserver = new RecordingLockPreserver();

        var applied = CreateRefresher(lockPreserver).TryApply(
            logic,
            visible,
            character,
            (roster, troop) =>
            {
                int index = roster.FindIndexOfTroop(troop);
                roster.SetElementNumber(index, 3);
                roster.InitializeCachedData();
            },
            () => notificationCount++);

        Assert.True(applied);
        Assert.Equal(1, notificationCount);
        Assert.False(logic.IsThereAnyChanges());
        Assert.Same(logic, Assert.Single(lockPreserver.Resets));
        AssertRoster(logic.CurrentData.RightMemberRoster, character, 3, 1);
        AssertRoster(logic._initialData.RightMemberRoster, character, 3, 1);
        Assert.Equal(-1, logic.CurrentData.LeftMemberRoster.FindIndexOfTroop(character));
    }

    /// <summary>
    /// Vanilla transfers keep a moved-out stack as a zero-count element. A heal on the server's copy of the
    /// stack must follow the troops into the garrison, also after a popup cancel, so Done commits it (#4563).
    /// </summary>
    [Fact]
    public void ServerHeal_AfterFullTransfer_CommitsHealToDestination()
    {
        var character = new CharacterObject();
        var logic = CreateLogic(character, 22, 22, out var visible);
        Transfer(visible, logic.CurrentData.LeftMemberRoster, character, 22, 22);
        logic.SavePartyScreenData();
        int notificationCount = 0;

        CreateRefresher().TryApply(
            logic,
            visible,
            character,
            (roster, troop) => roster.AddToCounts(troop, 0, false, -1),
            () => notificationCount++);
        logic.ResetToLastSavedPartyScreenData(false);

        var serverParty = CreateRoster(character, 22, 21);
        var serverGarrison = TroopRoster.CreateDummyTroopRoster();
        Assert.Equal(0, notificationCount);
        Assert.True(CommitDone(
            character,
            (visible, logic._initialData.RightMemberRoster, serverParty),
            (logic.CurrentData.LeftMemberRoster, logic._initialData.LeftMemberRoster, serverGarrison)));
        Assert.Equal(-1, serverParty.FindIndexOfTroop(character));
        AssertRoster(serverGarrison, character, 22, 21);
    }

    /// <summary>
    /// A wound on more troops than a partial transfer left behind lands on the troops it moved.
    /// </summary>
    [Fact]
    public void ServerWound_AfterPartialTransfer_CommitsExcessWoundToDestination()
    {
        var character = new CharacterObject();
        var logic = CreateLogic(character, 30, 0, out var visible);
        Transfer(visible, logic.CurrentData.LeftMemberRoster, character, 20, 0);
        int notificationCount = 0;

        CreateRefresher().TryApply(
            logic,
            visible,
            character,
            (roster, troop) => roster.AddToCounts(troop, 0, false, 15),
            () => notificationCount++);

        var serverParty = CreateRoster(character, 30, 15);
        var serverGarrison = TroopRoster.CreateDummyTroopRoster();
        Assert.Equal(0, notificationCount);
        Assert.True(CommitDone(
            character,
            (visible, logic._initialData.RightMemberRoster, serverParty),
            (logic.CurrentData.LeftMemberRoster, logic._initialData.LeftMemberRoster, serverGarrison)));
        AssertRoster(serverParty, character, 10, 10);
        AssertRoster(serverGarrison, character, 20, 5);
    }

    /// <summary>
    /// Daily training xp for a stack the player already moved out must reach the moved troops on Done.
    /// </summary>
    [Fact]
    public void ServerXp_AfterFullTransfer_CommitsXpToDestination()
    {
        var character = new CharacterObject();
        var logic = CreateLogic(character, 22, 0, out var visible);
        visible.AddXpToTroop(character, 100);
        logic._initialData.RightMemberRoster.AddXpToTroop(character, 100);
        Transfer(visible, logic.CurrentData.LeftMemberRoster, character, 22, 0, 100);
        int notificationCount = 0;

        CreateRefresher().TryApply(
            logic,
            visible,
            character,
            (roster, troop) => roster.SetElementXp(roster.FindIndexOfTroop(troop), 150),
            () => notificationCount++);

        var serverParty = CreateRoster(character, 22, 0, 150);
        var serverGarrison = TroopRoster.CreateDummyTroopRoster();
        Assert.Equal(0, notificationCount);
        Assert.True(CommitDone(
            character,
            (visible, logic._initialData.RightMemberRoster, serverParty),
            (logic.CurrentData.LeftMemberRoster, logic._initialData.LeftMemberRoster, serverGarrison)));
        Assert.Equal(-1, serverParty.FindIndexOfTroop(character));
        AssertRoster(serverGarrison, character, 22, 0);
        Assert.Equal(150, serverGarrison.GetElementXp(serverGarrison.FindIndexOfTroop(character)));
    }

    /// <summary>
    /// Upgrading a stack moves nothing to the garrison, so a heal on it cant land on the garrison's own stack.
    /// </summary>
    [Fact]
    public void ServerHeal_AfterUpgrade_ResetsWithoutTouchingOppositeStack()
    {
        var character = new CharacterObject();
        var upgraded = new CharacterObject();
        var logic = CreateLogic(character, 5, 5, out var visible);
        logic.CurrentData.LeftMemberRoster.AddToCounts(character, 10, false, 5);
        logic._initialData.LeftMemberRoster.AddToCounts(character, 10, false, 5);
        visible.AddToCounts(character, -5, false, -5);
        visible.AddToCounts(upgraded, 5, false, 5);
        int notificationCount = 0;

        CreateRefresher().TryApply(
            logic,
            visible,
            character,
            (roster, troop) => roster.AddToCounts(troop, 0, false, -1),
            () => notificationCount++);

        Assert.Equal(1, notificationCount);
        AssertRoster(logic.CurrentData.LeftMemberRoster, character, 10, 5);
    }

    /// <summary>
    /// Moving the healthy troops and upgrading the wounded rest leaves no moved troop to heal, so a heal resets
    /// instead of healing the garrison's own wounded.
    /// </summary>
    [Fact]
    public void ServerHeal_AfterHealthyTransferAndWoundedUpgrade_ResetsWithoutTouchingOppositeStack()
    {
        var character = new CharacterObject();
        var upgraded = new CharacterObject();
        var logic = CreateLogic(character, 10, 5, out var visible);
        logic.CurrentData.LeftMemberRoster.AddToCounts(character, 10, false, 5);
        logic._initialData.LeftMemberRoster.AddToCounts(character, 10, false, 5);
        Transfer(visible, logic.CurrentData.LeftMemberRoster, character, 5, 0);
        visible.AddToCounts(character, -5, false, -5);
        visible.AddToCounts(upgraded, 5, false, 5);
        int notificationCount = 0;

        CreateRefresher().TryApply(
            logic,
            visible,
            character,
            (roster, troop) => roster.AddToCounts(troop, 0, false, -1),
            () => notificationCount++);

        Assert.Equal(1, notificationCount);
        AssertRoster(logic.CurrentData.LeftMemberRoster, character, 10, 5);
    }

    /// <summary>
    /// Recruiting healthy prisoners of the troop can make the counts look like a pure transfer after the wounded
    /// were upgraded, so the recorded upgrade and recruit still reset instead of healing the garrison's own wounded.
    /// </summary>
    [Fact]
    public void ServerHeal_AfterTransferUpgradeAndRecruit_ResetsWithoutTouchingOppositeStack()
    {
        var character = new CharacterObject();
        var upgraded = new CharacterObject();
        var logic = CreateLogic(character, 10, 5, out var visible);
        var prisoners = logic.PrisonerRosters[(int)PartyScreenLogic.PartyRosterSide.Right];
        prisoners.AddToCounts(character, 5);
        logic._initialData.RightPrisonerRoster.AddToCounts(character, 5);
        logic.CurrentData.LeftMemberRoster.AddToCounts(character, 10, false, 5);
        logic._initialData.LeftMemberRoster.AddToCounts(character, 10, false, 5);
        Transfer(visible, logic.CurrentData.LeftMemberRoster, character, 5, 0);
        visible.AddToCounts(character, -5, false, -5);
        visible.AddToCounts(upgraded, 5, false, 5);
        logic.CurrentData.UpgradedTroopsHistory.Add(Tuple.Create(character, upgraded, 5));
        prisoners.AddToCounts(character, -5);
        visible.AddToCounts(character, 5);
        logic.CurrentData.RecruitedPrisonersHistory.Add(Tuple.Create(character, 5));
        int notificationCount = 0;

        CreateRefresher().TryApply(
            logic,
            visible,
            character,
            (roster, troop) => roster.AddToCounts(troop, 0, false, -1),
            () => notificationCount++);

        Assert.Equal(1, notificationCount);
        AssertRoster(logic.CurrentData.LeftMemberRoster, character, 10, 5);
    }

    /// <summary>
    /// A stack the server removes with no pending edit carries no xp into the opposite side's own stack.
    /// </summary>
    [Fact]
    public void ServerRemovesUneditedStack_LeavesOppositeStackXp()
    {
        var character = new CharacterObject();
        var logic = CreateLogic(character, 5, 0, out var visible);
        visible.AddXpToTroop(character, 100);
        logic._initialData.RightMemberRoster.AddXpToTroop(character, 100);
        logic.CurrentData.LeftMemberRoster.AddToCounts(character, 10, false, 0, 200);
        logic._initialData.LeftMemberRoster.AddToCounts(character, 10, false, 0, 200);
        int notificationCount = 0;

        CreateRefresher().TryApply(
            logic,
            visible,
            character,
            (roster, troop) => roster.AddToCounts(troop, -5),
            () => notificationCount++);

        var garrison = logic.CurrentData.LeftMemberRoster;
        Assert.Equal(0, notificationCount);
        Assert.Equal(-1, visible.FindIndexOfTroop(character));
        Assert.Equal(200, garrison.GetElementXp(garrison.FindIndexOfTroop(character)));
    }

    /// <summary>
    /// Prisoner healing uses the same transfer layout, so a heal follows prisoners moved to the dungeon.
    /// </summary>
    [Fact]
    public void ServerPrisonerHeal_AfterFullTransfer_CommitsHealToDestination()
    {
        var prisoner = new CharacterObject();
        var logic = CreateLogic(new CharacterObject(), 5, 0, out _);
        var prisoners = logic.PrisonerRosters[(int)PartyScreenLogic.PartyRosterSide.Right];
        prisoners.AddToCounts(prisoner, 10, false, 10);
        logic._initialData.RightPrisonerRoster.AddToCounts(prisoner, 10, false, 10);
        Transfer(prisoners, logic.CurrentData.LeftPrisonerRoster, prisoner, 10, 10);
        int notificationCount = 0;

        CreateRefresher().TryApply(
            logic,
            prisoners,
            prisoner,
            (roster, troop) => roster.AddToCounts(troop, 0, false, -2),
            () => notificationCount++);

        var serverPrisoners = CreateRoster(prisoner, 10, 8);
        var serverDungeon = TroopRoster.CreateDummyTroopRoster();
        Assert.Equal(0, notificationCount);
        Assert.True(CommitDone(
            prisoner,
            (prisoners, logic._initialData.RightPrisonerRoster, serverPrisoners),
            (logic.CurrentData.LeftPrisonerRoster, logic._initialData.LeftPrisonerRoster, serverDungeon)));
        Assert.Equal(-1, serverPrisoners.FindIndexOfTroop(prisoner));
        AssertRoster(serverDungeon, prisoner, 10, 8);
    }

    [Fact]
    public void ServerUpdate_RefreshesSavedPopupStateWithoutNotification()
    {
        var character = new CharacterObject();
        var logic = CreateLogic(character, 5, 1, out var visible);
        visible.AddToCounts(character, -1);
        logic.SavePartyScreenData();
        visible.AddToCounts(character, -1);
        int notificationCount = 0;

        var applied = CreateRefresher().TryApply(
            logic,
            visible,
            character,
            Heal,
            () => notificationCount++);

        logic.ResetToLastSavedPartyScreenData(false);

        Assert.True(applied);
        Assert.Equal(0, notificationCount);
        AssertRoster(visible, character, 4, 0);
    }

    [Fact]
    public void ServerUpdate_DoesNotResetUnrelatedItemRoster()
    {
        var character = new CharacterObject();
        var logic = CreateLogic(character, 5, 1, out var visible);
        var item = new ItemObject();
        var currentItems = new ItemRoster();
        currentItems.AddToCounts(new EquipmentElement(item, null), 2);
        logic.CurrentData.RightItemRoster = currentItems;
        logic._initialData.RightItemRoster = new ItemRoster();
        logic._initialData.RightItemRoster.AddToCounts(new EquipmentElement(item, null), 1);

        var applied = CreateRefresher().TryApply(logic, visible, character, Heal, () => { });

        Assert.True(applied);
        Assert.Equal(2, currentItems.GetElementCopyAtIndex(0).Amount);
    }

    [Fact]
    public void ServerUpdate_RefreshesVisibleCloneFromOwnerRoster()
    {
        var character = new CharacterObject();
        var logic = CreateLogic(character, 5, 1, out var visible);
        var authoritative = TroopRoster.CreateDummyTroopRoster();
        authoritative.AddToCounts(character, 5, false, 1);
        visible.AddToCounts(character, -2);
        var refresher = new PartyScreenRosterRefresher(
            new FixedBaselineProvider(logic._initialData.RightMemberRoster),
            new RecordingLockPreserver());

        var applied = refresher.TryApply(logic, authoritative, character, Heal, () => { });

        Assert.True(applied);
        AssertRoster(visible, character, 3, 0);
        AssertRoster(logic._initialData.RightMemberRoster, character, 5, 0);
    }

    [Fact]
    public void FindSelectionReplacement_MatchesCharacterSideAndType()
    {
        var character = new CharacterObject();
        var expected = new SelectionCandidate(
            character,
            PartyScreenLogic.PartyRosterSide.Right,
            PartyScreenLogic.TroopType.Member);
        var selection = expected.Identity;

        var replacement = PartyScreenRosterRefresher.FindSelectionReplacement(
            new[]
            {
                new SelectionCandidate(
                    new CharacterObject(),
                    PartyScreenLogic.PartyRosterSide.Right,
                    PartyScreenLogic.TroopType.Member),
                expected
            },
            new[]
            {
                new SelectionCandidate(
                    character,
                    PartyScreenLogic.PartyRosterSide.Right,
                    PartyScreenLogic.TroopType.Prisoner)
            },
            new[]
            {
                new SelectionCandidate(
                    character,
                    PartyScreenLogic.PartyRosterSide.Left,
                    PartyScreenLogic.TroopType.Member)
            },
            Array.Empty<SelectionCandidate>(),
            selection,
            candidate => candidate.Identity);

        Assert.Same(expected, replacement);
    }

    [Fact]
    public void FindSelectionReplacement_ReturnsNullWhenSelectionWasRemoved()
    {
        var selection = new PartyScreenSelectionIdentity(
            new CharacterObject(),
            PartyScreenLogic.PartyRosterSide.Right,
            PartyScreenLogic.TroopType.Member);

        var replacement = PartyScreenRosterRefresher.FindSelectionReplacement(
            Array.Empty<SelectionCandidate>(),
            Array.Empty<SelectionCandidate>(),
            Array.Empty<SelectionCandidate>(),
            Array.Empty<SelectionCandidate>(),
            selection,
            candidate => candidate.Identity);

        Assert.Null(replacement);
    }

    [Fact]
    public void ItemServerUpdate_RefreshesSavedPopupStateWithoutSavingPendingChanges()
    {
        var character = new CharacterObject();
        var logic = CreateLogic(character, 5, 1, out var visible);
        var item = new ItemObject();
        var element = new EquipmentElement(item, null);
        logic.CurrentData.RightItemRoster = new ItemRoster();
        logic.CurrentData.RightItemRoster.AddToCounts(element, 5);
        logic._initialData.RightItemRoster.AddToCounts(element, 5);
        logic.SavePartyScreenData();
        logic.CurrentData.RightItemRoster.AddToCounts(element, -1);
        visible.AddToCounts(character, -1);

        var applied = CreateRefresher().TryApply(
            logic,
            logic.CurrentData.RightItemRoster,
            roster => roster.AddToCounts(element, 2));

        logic.ResetToLastSavedPartyScreenData(false);

        Assert.True(applied);
        Assert.Equal(7, logic.CurrentData.RightItemRoster.GetElementCopyAtIndex(0).Amount);
        Assert.Equal(7, logic._initialData.RightItemRoster.GetElementCopyAtIndex(0).Amount);
        AssertRoster(visible, character, 5, 1);
    }

    [Fact]
    public void ItemServerClear_RefreshesSavedPopupStateWithoutSavingPendingChanges()
    {
        var character = new CharacterObject();
        var logic = CreateLogic(character, 5, 1, out var visible);
        var item = new ItemObject();
        var element = new EquipmentElement(item, null);
        logic.CurrentData.RightItemRoster = new ItemRoster();
        logic.CurrentData.RightItemRoster.AddToCounts(element, 5);
        logic._initialData.RightItemRoster.AddToCounts(element, 5);
        logic.SavePartyScreenData();
        logic.CurrentData.RightItemRoster.AddToCounts(element, -1);
        visible.AddToCounts(character, -1);

        var applied = CreateRefresher().TryApply(
            logic,
            logic.CurrentData.RightItemRoster,
            roster => roster.Clear());

        logic.ResetToLastSavedPartyScreenData(false);

        Assert.True(applied);
        Assert.Empty(logic.CurrentData.RightItemRoster);
        Assert.Empty(logic._initialData.RightItemRoster);
        AssertRoster(visible, character, 5, 1);
    }

    /// <summary>
    /// A talk started from the party screen is the active state on top of it. A prisoner the server
    /// frees during the talk (a recruited lord's companion) must leave the screen data too, otherwise
    /// the Done that vanilla runs on resume resets him back into the live prison roster.
    /// </summary>
    [Fact]
    public void ServerRelease_DuringPartyScreenTalk_StaysOutAfterDoneReset()
    {
        var prisoner = new CharacterObject();
        var logic = CreateLogic(new CharacterObject(), 5, 0, out _);
        var prisoners = logic.PrisonerRosters[(int)PartyScreenLogic.PartyRosterSide.Right];
        prisoners.AddToCounts(prisoner, 1);
        logic._initialData.RightPrisonerRoster.AddToCounts(prisoner, 1);

        var gameStates = new GameStateManager(null, GameStateManager.GameStateManagerType.Game);
        gameStates._gameStates.Add(new PartyState { PartyScreenLogic = logic });
        gameStates._gameStates.Add(ObjectHelper.SkipConstructor<TaleWorlds.MountAndBlade.MissionState>());

        // Same fallback as TroopRosterDeltaHandler when no open screen takes the change.
        var openLogic = PartyScreenRosterRefresher.GetOpenLogic(gameStates);
        if (!CreateRefresher().TryApply(openLogic, prisoners, prisoner, Release, () => { }))
            Release(prisoners, prisoner);

        logic.Reset(true);

        Assert.Same(logic, openLogic);
        Assert.Equal(-1, prisoners.FindIndexOfTroop(prisoner));
        Assert.Equal(-1, logic._initialData.RightPrisonerRoster.FindIndexOfTroop(prisoner));
    }

    /// <summary>
    /// With no party screen in the stack the change goes straight to the live roster.
    /// </summary>
    [Fact]
    public void GetOpenLogic_NoPartyState_ReturnsNull()
    {
        var gameStates = new GameStateManager(null, GameStateManager.GameStateManagerType.Game);
        gameStates._gameStates.Add(ObjectHelper.SkipConstructor<TaleWorlds.MountAndBlade.MissionState>());

        Assert.Null(PartyScreenRosterRefresher.GetOpenLogic(gameStates));
        Assert.Null(PartyScreenRosterRefresher.GetOpenLogic(null));
    }

    private static void Release(TroopRoster roster, CharacterObject troop)
    {
        roster.AddToCounts(troop, -roster.GetTroopCount(troop), false, 0, 0, true);
        roster.InitializeCachedData();
    }

    private static PartyScreenLogic CreateLogic(
        CharacterObject character,
        int number,
        int wounded,
        out TroopRoster visible)
    {
        var logic = new PartyScreenLogic();
        visible = TroopRoster.CreateDummyTroopRoster();
        var leftMembers = TroopRoster.CreateDummyTroopRoster();
        var rightPrisoners = TroopRoster.CreateDummyTroopRoster();
        var leftPrisoners = TroopRoster.CreateDummyTroopRoster();

        visible.AddToCounts(character, number, false, wounded);
        logic.MemberRosters[(int)PartyScreenLogic.PartyRosterSide.Right] = visible;
        logic.MemberRosters[(int)PartyScreenLogic.PartyRosterSide.Left] = leftMembers;
        logic.PrisonerRosters[(int)PartyScreenLogic.PartyRosterSide.Right] = rightPrisoners;
        logic.PrisonerRosters[(int)PartyScreenLogic.PartyRosterSide.Left] = leftPrisoners;
        logic.CurrentData.BindRostersFrom(
            visible,
            rightPrisoners,
            leftMembers,
            leftPrisoners,
            null,
            null);
        logic._initialData.InitializeCopyFrom(null, null);
        logic._initialData.RightMemberRoster.AddToCounts(character, number, false, wounded);
        return logic;
    }

    // Mirrors vanilla TransferTroop: the source keeps a zero-count element and its xp.
    private static void Transfer(
        TroopRoster source,
        TroopRoster destination,
        CharacterObject character,
        int number,
        int wounded,
        int xp = 0)
    {
        source.AddToCounts(character, -number, false, -wounded, 0, false);
        destination.AddToCounts(character, number, false, wounded, xp, false);
    }

    // Builds an ownerless roster holding one stack, standing in for a server roster.
    private static TroopRoster CreateRoster(CharacterObject character, int number, int wounded, int xp = 0)
    {
        var roster = TroopRoster.CreateDummyTroopRoster();
        roster.AddToCounts(character, number, false, wounded, xp);
        return roster;
    }

    // Packs each screen roster against its snapshot and applies the deltas to the server rosters, as Done does.
    private static bool CommitDone(
        CharacterObject character,
        params (TroopRoster current, TroopRoster initial, TroopRoster server)[] rosters)
    {
        uint characterId = 1;
        var resolved = character;
        var objectManager = new Mock<IObjectManager>();
        objectManager.Setup(m => m.TryGetHandleWithLogging(character, out characterId)).Returns(true);
        objectManager.Setup(m => m.TryGetObjectWithLogging(characterId, out resolved)).Returns(true);
        var troopRosters = new TroopRosterInterface(
            objectManager.Object,
            new NullTroopRosterLogger(),
            new Mock<IRecruitmentSideEffects>().Object);

        return troopRosters.TryApplyTroopRosterDeltas(rosters
            .Select(roster => (roster.server, troopRosters.PackTroopRosterDelta(roster.current, roster.initial)))
            .ToList());
    }

    private static PartyScreenRosterRefresher CreateRefresher(RecordingLockPreserver lockPreserver = null)
        => new PartyScreenRosterRefresher(
            new PartyScreenRosterBaselineProvider(),
            lockPreserver ?? new RecordingLockPreserver());

    private static void Heal(TroopRoster roster, CharacterObject troop)
    {
        int index = roster.FindIndexOfTroop(troop);
        roster.SetElementWoundedNumber(index, 0);
        roster.InitializeCachedData();
    }

    private static void AssertRoster(
        TroopRoster roster,
        CharacterObject character,
        int number,
        int wounded)
    {
        int index = roster.FindIndexOfTroop(character);
        Assert.True(index >= 0);
        Assert.Equal(number, roster.GetElementNumber(index));
        Assert.Equal(wounded, roster.GetElementWoundedNumber(index));
    }

    // Discards roster diagnostics; ITroopRosterLogger is internal, so Moq cant proxy it.
    private sealed class NullTroopRosterLogger : ITroopRosterLogger
    {
        // Ignores a roster-keyed diagnostic.
        public void Debug(TroopRoster troopRoster, string messageTemplate, params object[] propertyValues) { }

        // Ignores an id-keyed diagnostic.
        public void Debug(string troopRosterId, string messageTemplate, params object[] propertyValues) { }
    }

    private sealed class RecordingLockPreserver : IPartyScreenLockPreserver
    {
        public List<PartyScreenLogic> Resets { get; } = new List<PartyScreenLogic>();

        public void Reset(PartyScreenLogic logic, Action reset)
        {
            Resets.Add(logic);
            reset();
        }
    }

    private sealed class FixedBaselineProvider : IPartyScreenRosterBaselineProvider
    {
        private readonly TroopRoster baseline;

        public FixedBaselineProvider(TroopRoster baseline)
        {
            this.baseline = baseline;
        }

        public TroopRoster GetBaselineRoster(PartyScreenLogic logic, TroopRoster roster) => baseline;

        public ItemRoster GetBaselineRoster(PartyScreenLogic logic, ItemRoster roster) => null!;
    }

    private sealed class SelectionCandidate
    {
        public PartyScreenSelectionIdentity Identity { get; }

        public SelectionCandidate(
            CharacterObject character,
            PartyScreenLogic.PartyRosterSide side,
            PartyScreenLogic.TroopType type)
        {
            Identity = new PartyScreenSelectionIdentity(character, side, type);
        }
    }
}
