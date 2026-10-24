#if COOP_NAVAL
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

    // The game's shortest, middle and longest ship deployment lengths (mission_ships.xml).
    private static readonly float[] GameShipLengths = { 35f, 45.85f, 67.5f };
    private const float VanillaOffset = 400f;
    private const float PathStart = -500f;

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
    public void LongestTeamRange_IsVanillasMarginOnTheLongestShip()
    {
        Assert.Equal(1.1f * 67.5f, NavalPlayerDeploymentSlot.LongestTeamRange(GameShipLengths), 3);
    }

    [Theory]
    [InlineData(67.5f, 35f, 45.85f)]
    [InlineData(67.5f, 67.5f, 35f)]
    [InlineData(35f, 67.5f, 35f)]
    [InlineData(45.85f, 35f, 67.5f)]
    public void ShiftSpawnPathOffset_MixedFleets_KeepVanillasGapBetweenEveryRank(float first, float second, float third)
    {
        float longest = NavalPlayerDeploymentSlot.LongestTeamRange(GameShipLengths);
        float[] ranges = { Range(first), Range(second), Range(third) };

        for (int rank = 1; rank < ranges.Length; rank++)
        {
            float aheadOffset = Offset(rank - 1, ranges[rank - 1], longest);
            float ownOffset = Offset(rank, ranges[rank], longest);

            Assert.True(aheadOffset - ranges[rank - 1] - ownOffset >= NavalPlayerDeploymentSlot.InterTeamGap - 0.001f,
                $"rank {rank} fleet starts {aheadOffset - ranges[rank - 1] - ownOffset} m behind the fleet ahead");
        }
    }

    [Fact]
    public void ShiftSpawnPathOffset_FirstRank_KeepsTheVanillaOffset()
    {
        Assert.Equal(VanillaOffset, NavalPlayerDeploymentSlot.ShiftSpawnPathOffset(VanillaOffset, 0, Range(35f), Range(67.5f), PathStart));
    }

    [Fact]
    public void ShiftSpawnPathOffset_ShortPath_KeepsTheWholeFleetOnThePath()
    {
        float ownRange = Range(45.85f);
        float pathStart = VanillaOffset - 150f;

        float offset = NavalPlayerDeploymentSlot.ShiftSpawnPathOffset(VanillaOffset, 3, ownRange, Range(67.5f), pathStart);

        Assert.Equal(pathStart + ownRange, offset, 3);
    }

    [Fact]
    public void ShiftSpawnPathOffset_PathShorterThanTheFleet_NeverMovesItAheadOfVanilla()
    {
        float offset = NavalPlayerDeploymentSlot.ShiftSpawnPathOffset(VanillaOffset, 1, Range(67.5f), Range(67.5f),
            VanillaOffset - 10f);

        Assert.Equal(VanillaOffset, offset);
    }

    private static float Range(float shipLength) => NavalPlayerDeploymentSlot.LongestTeamRange(new[] { shipLength });

    private static float Offset(int rank, float ownRange, float longest) =>
        NavalPlayerDeploymentSlot.ShiftSpawnPathOffset(VanillaOffset, rank, ownRange, longest, PathStart);
}
#endif
