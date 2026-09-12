using GameInterface.Services.Party;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using Xunit;

namespace GameInterface.Tests.Services.Party;

public class PartyScreenRosterBaselineProviderTests
{
    [Fact]
    public void GetBaselineRoster_MapsEveryPartyScreenRoster()
    {
        var logic = new PartyScreenLogic();
        var rightMember = new TroopRoster();
        var leftMember = new TroopRoster();
        var rightPrisoner = new TroopRoster();
        var leftPrisoner = new TroopRoster();
        logic.MemberRosters[(int)PartyScreenLogic.PartyRosterSide.Right] = rightMember;
        logic.MemberRosters[(int)PartyScreenLogic.PartyRosterSide.Left] = leftMember;
        logic.PrisonerRosters[(int)PartyScreenLogic.PartyRosterSide.Right] = rightPrisoner;
        logic.PrisonerRosters[(int)PartyScreenLogic.PartyRosterSide.Left] = leftPrisoner;
        logic._initialData.RightMemberRoster = new TroopRoster();
        logic._initialData.LeftMemberRoster = new TroopRoster();
        logic._initialData.RightPrisonerRoster = new TroopRoster();
        logic._initialData.LeftPrisonerRoster = new TroopRoster();
        var rightItems = new ItemRoster();
        logic.CurrentData.RightItemRoster = rightItems;
        logic._initialData.RightItemRoster = new ItemRoster();

        var provider = new PartyScreenRosterBaselineProvider();

        Assert.Same(logic._initialData.RightMemberRoster, provider.GetBaselineRoster(logic, rightMember));
        Assert.Same(logic._initialData.LeftMemberRoster, provider.GetBaselineRoster(logic, leftMember));
        Assert.Same(logic._initialData.RightPrisonerRoster, provider.GetBaselineRoster(logic, rightPrisoner));
        Assert.Same(logic._initialData.LeftPrisonerRoster, provider.GetBaselineRoster(logic, leftPrisoner));
        Assert.Null(provider.GetBaselineRoster(logic, new TroopRoster()));
        Assert.Same(logic._initialData.RightItemRoster, provider.GetBaselineRoster(logic, rightItems));
        Assert.Null(provider.GetBaselineRoster(logic, new ItemRoster()));
    }
}
