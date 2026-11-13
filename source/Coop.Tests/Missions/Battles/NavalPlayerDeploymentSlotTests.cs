#if COOP_NAVAL
using Missions.Naval;
using System;
using System.Collections.Generic;
using System.Linq;
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

        var front = NavalPlayerDeploymentSlot.SlotFor(path, 0, Longest);
        var behind = NavalPlayerDeploymentSlot.SlotFor(path, 1, Longest);

        Assert.Equal(SideBase(path), front.Offset, 3);
        Assert.True(front.Offset - bigFleetRange - behind.Offset >= NavalPlayerDeploymentSlot.InterTeamGap - 0.01f,
            $"rank 1 at {behind.Offset} is inside vanilla's gap behind rank 0's fleet at {front.Offset}");
        Assert.True(front.Fits && behind.Fits);
    }

    // M11 phase 3: the allied AI fleet goes a gap ahead of rank 0, as vanilla puts allies in front of the player team, and the
    // ranks go behind over the free segments as vanilla places teams; once the path runs out behind, the next rank goes ahead of
    // the allies. Rows (one free segment from -1000 or -100 to 1000, base -199 or -100): room behind; the base at the path start
    // (R49 C2), where rank 0 takes the first room and ranks 1 and 2 go ahead of the allies; the same with the opposing base
    // close, which holds rank 2 short of it; and a blocked stretch behind the base, which rank 0 skips like vanilla.
    [Theory]
    [InlineData(new[] { -1000f, 1000f }, -199f, 1000f, -92.75f, new[] { -199f, -305.25f, -411.5f }, true)]
    [InlineData(new[] { -100f, 1000f }, -100f, 1000f, 80.5f, new[] { -25.75f, 186.75f, 293f }, true)]
    [InlineData(new[] { -100f, 1000f }, -100f, 250f, 80.5f, new[] { -25.75f, 186.75f, 250f }, false)]
    [InlineData(new[] { -1000f, -250f, -230f, 1000f }, -199f, 1000f, -143.75f, new[] { -250f, -356.25f }, true)]
    public void Slots_PutTheAlliesAheadOfRankZero_AndTheRanksBehindThenAhead(float[] freeSegments, float sideBase,
        float forwardLimit, float expectedAlly, float[] expectedRanks, bool expectedFits)
    {
        var segments = Enumerable.Range(0, freeSegments.Length / 2)
            .Select(index => (freeSegments[2 * index], freeSegments[(2 * index) + 1])).ToList();

        var slots = NavalPlayerDeploymentSlot.Slots(segments, sideBase, forwardLimit, Longest, expectedRanks.Length, out float ally,
            out bool fits);

        Assert.Equal(expectedAlly, ally, 2);
        Assert.Equal(expectedRanks, slots.Select(slot => (float)Math.Round(slot, 2)));
        Assert.Equal(expectedFits, fits);
    }

    // R49 C3: a slot depends only on its rank, so a client that counts a late joiner or misses a disconnected player derives the
    // same slot for an earlier rank, also when the ranks have gone ahead of the allies.
    [Theory]
    [InlineData(-1000f, -199f)]
    [InlineData(-100f, -100f)]
    public void Slots_OfARank_DoNotDependOnHowManyRanksTheClientCounts(float pathStart, float sideBase)
    {
        var segments = new List<(float, float)> { (pathStart, 1000f) };

        var fewer = NavalPlayerDeploymentSlot.Slots(segments, sideBase, 1000f, Longest, 2, out _, out _);
        var more = NavalPlayerDeploymentSlot.Slots(segments, sideBase, 1000f, Longest, 4, out _, out _);

        Assert.Equal(fewer, more.Take(2));
    }

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
