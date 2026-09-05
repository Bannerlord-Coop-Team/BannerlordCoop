using Common.Util;
using GameInterface.Services.Clans;
using GameInterface.Services.Players;
using Moq;
using TaleWorlds.CampaignSystem;
using Xunit;

namespace GameInterface.Tests.Services.Clans;

public class ClanMemberGroupingTests
{
    private readonly Mock<IPlayerManager> players = new();

    [Fact]
    public void ViewingPlayer_StaysWithOwnFamily()
    {
        var viewer = CreateHero();
        players.Setup(manager => manager.Contains(viewer)).Returns(true);

        Assert.Equal(ClanMemberGroup.Family, Group(viewer, viewer));
    }

    [Fact]
    public void OtherPlayer_GoesInPlayersEvenWhenRelated()
    {
        var viewer = CreateHero();
        var player = CreateHero();
        viewer._spouse = player;
        players.Setup(manager => manager.Contains(player)).Returns(true);

        Assert.Equal(ClanMemberGroup.Players, Group(player, viewer));
    }

    [Fact]
    public void Family_IsRelativeToViewerInsteadOfClanLeader()
    {
        var viewer = CreateHero();
        var otherPlayer = CreateHero();
        var viewersChild = CreateHero();
        viewersChild._father = viewer;
        var otherChild = CreateHero();
        otherChild._father = otherPlayer;

        Assert.Equal(ClanMemberGroup.Family, Group(viewersChild, viewer));
        Assert.Equal(ClanMemberGroup.OtherFamilies, Group(otherChild, viewer));
        Assert.Equal(ClanMemberGroup.Family, Group(otherChild, otherPlayer));
        Assert.Equal(ClanMemberGroup.OtherFamilies, Group(viewersChild, otherPlayer));
    }

    [Fact]
    public void SharedChild_StaysInEachViewingPlayersFamily()
    {
        var firstPlayer = CreateHero();
        var secondPlayer = CreateHero();
        var child = CreateHero();
        child._father = firstPlayer;
        child._mother = secondPlayer;

        Assert.Equal(ClanMemberGroup.Family, Group(child, firstPlayer));
        Assert.Equal(ClanMemberGroup.Family, Group(child, secondPlayer));
    }

    [Fact]
    public void ExtendedFamilyAndInLaws_StayInFamily()
    {
        var ancestor = CreateHero();
        var viewer = CreateHero();
        viewer._mother = ancestor;
        var sibling = CreateHero();
        sibling._mother = ancestor;
        var nephew = CreateHero();
        nephew._father = sibling;
        var siblingSpouse = CreateHero();
        siblingSpouse._spouse = sibling;
        var spouse = CreateHero();
        spouse._father = CreateHero();
        viewer._spouse = spouse;

        Assert.Equal(ClanMemberGroup.Family, Group(nephew, viewer));
        Assert.Equal(ClanMemberGroup.Family, Group(siblingSpouse, viewer));
        Assert.Equal(ClanMemberGroup.Family, Group(spouse._father, viewer));
    }

    [Fact]
    public void UnrelatedClanMember_IsIncludedInOtherFamilies()
    {
        Assert.Equal(ClanMemberGroup.OtherFamilies, Group(CreateHero(), CreateHero()));
    }

    private ClanMemberGroup Group(Hero member, Hero viewer)
    {
        return new ClanMemberGrouping(players.Object).GetGroup(member, viewer);
    }

    private static Hero CreateHero() => ObjectHelper.SkipConstructor<Hero>();
}
