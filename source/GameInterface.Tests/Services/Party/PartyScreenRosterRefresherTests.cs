using GameInterface.Services.Party;
using TaleWorlds.CampaignSystem;
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

        var applied = CreateRefresher().TryApply(
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
        AssertRoster(logic.CurrentData.RightMemberRoster, character, 3, 1);
        AssertRoster(logic._initialData.RightMemberRoster, character, 3, 1);
        Assert.Equal(-1, logic.CurrentData.LeftMemberRoster.FindIndexOfTroop(character));
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
            new FixedBaselineProvider(logic._initialData.RightMemberRoster));

        var applied = refresher.TryApply(logic, authoritative, character, Heal, () => { });

        Assert.True(applied);
        AssertRoster(visible, character, 3, 0);
        AssertRoster(logic._initialData.RightMemberRoster, character, 5, 0);
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

    private static PartyScreenRosterRefresher CreateRefresher()
        => new PartyScreenRosterRefresher(new PartyScreenRosterBaselineProvider());

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
}
