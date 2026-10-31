#if COOP_NAVAL
using Missions.Naval;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.Serialization;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
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
    private static readonly float Longest = NavalPlayerDeploymentSlot.LongestTeamRange(GameShipLengths);

    [Fact]
    public void RankOf_FirstPlayerOfTheSide_IsTheFrontSlot()
    {
        Assert.Equal(0, NavalPlayerDeploymentSlot.RankOf(Side, FirstPlayer, Players.Contains));
    }

    [Fact]
    public void RankOf_LaterPlayer_CountsOnlyThePlayerPartiesBeforeIt()
    {
        Assert.Equal(1, NavalPlayerDeploymentSlot.RankOf(Side, SecondPlayer, Players.Contains));
    }

    [Fact]
    public void PlayerCountOf_SkipsAiParties()
    {
        Assert.Equal(2, NavalPlayerDeploymentSlot.PlayerCountOf(Side, Players.Contains));
    }

    [Fact]
    public void LongestTeamRange_IsVanillasMarginOnTheLongestShip()
    {
        Assert.Equal(1.1f * 67.5f, Longest, 3);
    }

    // NavalShipsLogic.ComputeSpawnPathDeploymentOffset for two ships or fewer: -min(path, 400) / 2 + 1.
    [Theory]
    [InlineData(2000f, -199f)]
    [InlineData(300f, -149f)]
    public void SharedDeploymentOffset_IsVanillasSmallBattleOffset(float pathLength, float expected)
    {
        Assert.Equal(expected, NavalPlayerDeploymentSlot.SharedDeploymentOffset(pathLength), 3);
    }

    // R49 fix review C1: vanilla's offset deepens with the ships this client assigned, which is only its own fleet, so a
    // 4-hull rank 0 and a 1-hull rank 1 derived bases 70 m apart and rank 1's bow sat 40 m inside rank 0's fleet.
    [Fact]
    public void SlotFor_TwoClientsWithFleetsOfDifferentSizes_DeriveOneLayout()
    {
        var path = FreePath(2000f, 1000f);
        float bigFleetRange = NavalPlayerDeploymentSlot.LongestTeamRange(new[] { 67.5f });

        float front = NavalPlayerDeploymentSlot.SlotFor(path, 0, 2, Longest, out float frontBase, out _);
        float behind = NavalPlayerDeploymentSlot.SlotFor(path, 1, 2, Longest, out float behindBase, out _);

        Assert.Equal(frontBase, behindBase, 3);
        Assert.True(front - bigFleetRange - behind >= NavalPlayerDeploymentSlot.InterTeamGap - 0.01f,
            $"rank 1 at {behind} is inside vanilla's gap behind rank 0's fleet at {front}");
    }

    // R49: the spawn point sat near the path start, so vanilla's side base was the path start and nothing fit behind it.
    [Theory]
    [InlineData(100f)]
    [InlineData(150f)]
    public void PlaceSideFleets_BaseAtThePathStart_PacksTheFleetsForwardWithoutOverlap(float pivotOffset)
    {
        var path = FreePath(2000f, pivotOffset);

        var layout = Layout(path, playerCount: 2);

        Assert.True(layout[1] - Longest >= -pivotOffset - 0.01f, $"fleet 1 starts {layout[1] - Longest}, before the path");
        Assert.True(layout[0] - Longest - layout[1] >= NavalPlayerDeploymentSlot.InterTeamGap - 0.01f,
            $"fleet 0 at {layout[0]} and fleet 1 at {layout[1]} are closer than vanilla's gap");
        Assert.True(NavalPlayerDeploymentSlot.FitsPath(layout, Longest, -pivotOffset));
    }

    [Fact]
    public void PlaceSideFleets_RoomBehindTheBase_KeepsTheFrontFleetOnVanillasSlot()
    {
        var path = FreePath(2000f, 1000f);
        float baseOffset = SideBase(path);
        DefaultBattleMissionAgentSpawnLogic.ComputeTeamDeploymentOffsets(path, baseOffset, NavalPlayerDeploymentSlot.InterTeamGap,
            new[] { Longest }, out var vanillaSingleTeam);

        var layout = Layout(path, playerCount: 3);

        Assert.Equal(vanillaSingleTeam[0], layout[0], 3);
        Assert.Equal(layout[0] - Longest - NavalPlayerDeploymentSlot.InterTeamGap, layout[1], 3);
        Assert.Equal(layout[1] - Longest - NavalPlayerDeploymentSlot.InterTeamGap, layout[2], 3);
        Assert.True(NavalPlayerDeploymentSlot.FitsPath(layout, Longest, -1000f));
    }

    [Fact]
    public void PlaceSideFleets_BlockedSegmentBehindTheBase_SkipsItLikeVanilla()
    {
        var path = Path(2000f, 1000f, (-1000f, -250f), (-230f, 1000f));

        var layout = Layout(path, playerCount: 2);

        Assert.Equal(-250f, layout[0], 3);
        Assert.Equal(-250f - Longest - NavalPlayerDeploymentSlot.InterTeamGap, layout[1], 3);
    }

    [Fact]
    public void FitsPath_PathTooShortForTheFleets_IsFalse()
    {
        var path = FreePath(150f, 75f);

        var layout = Layout(path, playerCount: 2);

        Assert.False(NavalPlayerDeploymentSlot.FitsPath(layout, Longest, -75f));
    }

    private static float[] Layout(SpawnPathData path, int playerCount) =>
        NavalPlayerDeploymentSlot.PlaceSideFleets(path, SideBase(path), playerCount, Longest);

    // The deploying side base SlotFor lays the fleets from, vanilla's base for the shared offset.
    private static float SideBase(SpawnPathData path)
    {
        DefaultBattleMissionAgentSpawnLogic.ComputeDeploymentBaseOffsets(path,
            NavalPlayerDeploymentSlot.SharedDeploymentOffset(path.PathLength), out float deploying, out _);
        return deploying;
    }

    private static SpawnPathData FreePath(float pathLength, float pivotOffset) =>
        Path(pathLength, pivotOffset, (-pivotOffset, pathLength - pivotOffset));

    // SpawnPathData's constructor reads a native Path, so the test sets the fields the placement reads.
    private static SpawnPathData Path(float pathLength, float pivotOffset, params (float start, float end)[] freeSegments)
    {
        var path = (SpawnPathData)FormatterServices.GetUninitializedObject(typeof(SpawnPathData));
        var segments = new MBList<(float startOffset, float endOffset)>();
        foreach (var segment in freeSegments) segments.Add(segment);
        SetField(path, nameof(SpawnPathData.PathLength), pathLength);
        SetField(path, nameof(SpawnPathData.PivotOffset), pivotOffset);
        SetField(path, "_freeSegments", segments);
        return path;
    }

    private static void SetField(SpawnPathData path, string name, object value) =>
        typeof(SpawnPathData).GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .SetValue(path, value);
}
#endif
