using Missions.Battles;
using Xunit;

namespace Coop.Tests.Missions.Battles;

public class BattleTeamResolverTests
{
    private static readonly object Main = new object();
    private static readonly object Ally = new object();
    private static readonly object Other = new object();

    [Fact]
    public void Choose_OwnRecord_UsesTheSideMainTeam()
    {
        Assert.Same(Main, BattleTeamResolver.Choose(Main, Ally, Main, isOwn: true));
    }

    [Fact]
    public void Choose_AnotherOwnerOnOurSide_UsesTheAllyTeam()
    {
        Assert.Same(Ally, BattleTeamResolver.Choose(Main, Ally, Main, isOwn: false));
    }

    [Fact]
    public void Choose_AnotherOwnerOnTheOpposingSide_UsesThatSideMainTeam()
    {
        Assert.Same(Main, BattleTeamResolver.Choose(Main, Ally, Other, isOwn: false));
    }

    [Fact]
    public void Choose_AnotherOwnerOnOurSideWithoutAllyTeam_Waits()
    {
        Assert.Null(BattleTeamResolver.Choose(Main, null, Main, isOwn: false));
    }
}
