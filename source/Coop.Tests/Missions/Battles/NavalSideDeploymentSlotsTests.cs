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

public class NavalSideDeploymentSlotsTests
{
    private static readonly object FirstPlayer = new object();
    private static readonly object AiParty = new object();
    private static readonly object SecondPlayer = new object();
    private static readonly object[] Side = { FirstPlayer, AiParty, SecondPlayer };
    private static readonly HashSet<object> Players = new HashSet<object> { FirstPlayer, SecondPlayer };

    // The game's shortest, middle and longest ship deployment lengths (mission_ships.xml).
    private static readonly float[] GameShipLengths = { 35f, 45.85f, 67.5f };
    private static readonly float Longest = NavalSideDeploymentSlots.LongestTeamRange(GameShipLengths);

    [Fact]
    public void RankOf_FirstPlayerOfTheSide_IsTheFrontSlot()
    {
        Assert.Equal(0, NavalSideDeploymentSlots.RankOf(Side, FirstPlayer, Players.Contains));
    }

    [Fact]
    public void RankOf_LaterPlayer_CountsOnlyThePlayerPartiesBeforeIt()
    {
        Assert.Equal(1, NavalSideDeploymentSlots.RankOf(Side, SecondPlayer, Players.Contains));
    }

    [Fact]
    public void PlayerCountOf_SkipsAiParties()
    {
        Assert.Equal(2, NavalSideDeploymentSlots.PlayerCountOf(Side, Players.Contains));
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
        Assert.Equal(expected, NavalSideDeploymentSlots.SharedDeploymentOffset(pathLength), 3);
    }

    // M11 phase 3, on paths whose base vanilla's ComputeDeploymentBaseOffsets derives from the shared offset. Rank 0 takes
    // vanilla's slot behind the base, the next ranks go behind it, and once the path runs out behind they go ahead of the
    // allies' reserved slot, held a fleet and a gap short of the real opposing base; the allies go a gap ahead of rank 0.
    // Rows: room behind (base -199, opposing 199); the base at the path start (R49 C2, base -100, opposing 298); a short path
    // (base -149, opposing 149); a blocked stretch behind the base, which rank 0 skips like vanilla.
    [Theory]
    [InlineData(2000f, 1000f, new float[0], new[] { -199f, -305.25f, -411.5f },
        new[] { NavalSlotProblem.None, NavalSlotProblem.None, NavalSlotProblem.None }, -92.75f)]
    [InlineData(2000f, 100f, new float[0], new[] { -25.75f, 186.75f, 191.75f },
        new[] { NavalSlotProblem.None, NavalSlotProblem.None, NavalSlotProblem.HeldShortOfOpposingBase }, 80.5f)]
    [InlineData(300f, 150f, new float[0], new[] { -75.75f, 42.75f },
        new[] { NavalSlotProblem.None, NavalSlotProblem.HeldShortOfOpposingBase }, 30.5f)]
    [InlineData(2000f, 1000f, new[] { -1000f, -250f, -230f, 1000f }, new[] { -250f, -356.25f },
        new[] { NavalSlotProblem.None, NavalSlotProblem.None }, -143.75f)]
    public void Layout_PutsTheRanksBehindThenAhead_AndAllySlot_IsAGapAheadOfRankZero(float pathLength, float pivotOffset,
        float[] freeSegments, float[] expectedOffsets, NavalSlotProblem[] expectedProblems, float expectedAlly)
    {
        var path = freeSegments.Length == 0 ? FreePath(pathLength, pivotOffset) : Path(pathLength, pivotOffset,
            Enumerable.Range(0, freeSegments.Length / 2).Select(index => (freeSegments[2 * index], freeSegments[(2 * index) + 1])).ToArray());

        var layout = NavalSideDeploymentSlots.Layout(path, expectedOffsets.Length, Longest);
        var ally = NavalSideDeploymentSlots.AllySlot(path, layout.Slots[0].Offset, Longest, layout.ForwardLimit);

        Assert.Equal(expectedOffsets, layout.Slots.Select(slot => (float)Math.Round(slot.Offset, 2)));
        Assert.Equal(expectedProblems, layout.Slots.Select(slot => slot.Problem));
        Assert.Equal((expectedAlly, NavalSlotProblem.None), ((float)Math.Round(ally.Offset, 2), ally.Problem));
    }

    // R49 C3: a slot depends only on its rank, so a client that counts a late joiner or misses a disconnected player derives the
    // same slot for an earlier rank, also when the ranks have gone ahead of the allies.
    [Theory]
    [InlineData(1000f)]
    [InlineData(100f)]
    public void Layout_OfARank_DoesNotDependOnHowManyRanksTheClientCounts(float pivotOffset)
    {
        var path = FreePath(2000f, pivotOffset);

        var fewer = NavalSideDeploymentSlots.Layout(path, 2, Longest).Slots;
        var more = NavalSideDeploymentSlots.Layout(path, 4, Longest).Slots;

        Assert.Equal(fewer, more.Take(2));
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
