using Missions.Naval;
using System.Collections.Generic;
using Xunit;

namespace Coop.Tests.Missions.Battles;

public class NavalPlayerDeploymentSlotTests
{
    private static readonly object FirstPlayer = new object();
    private static readonly object AiParty = new object();
    private static readonly object SecondPlayer = new object();
    private static readonly object[] Side = { FirstPlayer, AiParty, SecondPlayer };
    private static readonly HashSet<object> Players = new HashSet<object> { FirstPlayer, SecondPlayer };

    [Fact]
    public void RankOf_FirstPlayerOfTheSide_KeepsTheVanillaSlot()
    {
        Assert.Equal(0, NavalPlayerDeploymentSlot.RankOf(Side, FirstPlayer, Players.Contains));
    }

    [Fact]
    public void RankOf_LaterPlayer_CountsOnlyThePlayerPartiesBeforeIt()
    {
        Assert.Equal(1, NavalPlayerDeploymentSlot.RankOf(Side, SecondPlayer, Players.Contains));
    }

    [Fact]
    public void ShiftSpawnPathOffset_MovesBackOneTeamDepthPlusTheVanillaGapPerRank()
    {
        Assert.Equal(100f, NavalPlayerDeploymentSlot.ShiftSpawnPathOffset(100f, 0, 44f));
        Assert.Equal(100f - (2 * (44f + NavalPlayerDeploymentSlot.InterTeamGap)),
            NavalPlayerDeploymentSlot.ShiftSpawnPathOffset(100f, 2, 44f));
    }
}
