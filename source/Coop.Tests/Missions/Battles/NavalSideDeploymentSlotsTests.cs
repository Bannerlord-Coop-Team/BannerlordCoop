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
    [Theory]
    // Room behind the base: base -199, opposing base 199.
    [InlineData(2000f, 1000f, new float[0], new[] { -199f, -305.25f, -411.5f },
        new[] { NavalSlotProblem.None, NavalSlotProblem.None, NavalSlotProblem.None }, -92.75f, NavalSlotProblem.None)]
    // The base at the path start (R49 C2): base -100, opposing base 298, so rank 2 is held short of it.
    [InlineData(2000f, 100f, new float[0], new[] { -25.75f, 186.75f, 191.75f },
        new[] { NavalSlotProblem.None, NavalSlotProblem.None, NavalSlotProblem.HeldShortOfOpposingBase }, 80.5f, NavalSlotProblem.None)]
    // A short path: base -149, opposing base 149.
    [InlineData(300f, 150f, new float[0], new[] { -75.75f, 42.75f },
        new[] { NavalSlotProblem.None, NavalSlotProblem.HeldShortOfOpposingBase }, 30.5f, NavalSlotProblem.None)]
    // A blocked stretch behind the base, which rank 0 skips like vanilla.
    [InlineData(2000f, 1000f, new[] { -1000f, -250f, -230f, 1000f }, new[] { -250f, -356.25f },
        new[] { NavalSlotProblem.None, NavalSlotProblem.None }, -143.75f, NavalSlotProblem.None)]
    // A blocked stretch just ahead of rank 0: the allies move past it, and rank 1, out of room behind, goes past the allies.
    [InlineData(2000f, 1000f, new[] { -400f, -250f, -150f, 1000f }, new[] { -250f, 30.5f },
        new[] { NavalSlotProblem.None, NavalSlotProblem.None }, -75.75f, NavalSlotProblem.None)]
    // Nothing free ahead of rank 0: the allies and rank 1 have no room on the path.
    [InlineData(2000f, 1000f, new[] { -400f, -250f }, new[] { -250f, -37.5f },
        new[] { NavalSlotProblem.None, NavalSlotProblem.NoRoomOnPath }, -143.75f, NavalSlotProblem.NoRoomOnPath)]
    // A path too short for the allies: base -99 and opposing base 99 hold the allies and rank 1 at -7.25.
    [InlineData(200f, 100f, new float[0], new[] { -25.75f, -7.25f },
        new[] { NavalSlotProblem.None, NavalSlotProblem.HeldShortOfOpposingBase }, -7.25f, NavalSlotProblem.HeldShortOfOpposingBase)]
    public void Layout_PutsTheRanksBehindThenAhead_AndTheAlliesAGapAheadOfRankZero(float pathLength, float pivotOffset,
        float[] freeSegments, float[] expectedOffsets, NavalSlotProblem[] expectedProblems, float expectedAlly,
        NavalSlotProblem expectedAllyProblem)
    {
        var path = Path(pathLength, pivotOffset, freeSegments);

        var layout = NavalSideDeploymentSlots.Layout(path, expectedOffsets.Length, Longest);
        var (ally, _) = NavalSideDeploymentSlots.AllySlot(path, ownRank: 1, ownPlanOffset: 0f, Longest, Longest);

        Assert.Equal(expectedOffsets, layout.Slots.Select(slot => (float)Math.Round(slot.Offset, 2)));
        Assert.Equal(expectedProblems, layout.Slots.Select(slot => slot.Problem));
        Assert.Equal((expectedAlly, expectedAllyProblem), ((float)Math.Round(ally.Offset, 2), ally.Problem));
    }

    // The allies plan from rank 0's real plan: the host's own when it is rank 0 (a lone player on vanilla's deeper plan for four
    // ships), else rank 0's shared slot. Rank 0's offset + 32 m gap + 50 m ally depth: -271 -> -189, -199 -> -117.
    [Theory]
    [InlineData(0, -271f, -189f)]
    [InlineData(1, -305.25f, -117f)]
    public void AllySlot_IsAGapAheadOfRankZerosRealPlan(int ownRank, float ownPlanOffset, float expectedAlly)
    {
        var (ally, opposingBase) = NavalSideDeploymentSlots.AllySlot(FreePath(2000f, 1000f), ownRank, ownPlanOffset,
            allyRange: 50f, Longest);

        Assert.Equal((expectedAlly, NavalSlotProblem.None, 199f), ((float)Math.Round(ally.Offset, 2), ally.Problem, opposingBase));
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

    // A path whose free segments are given as start and end pairs.
    private static SpawnPathData Path(float pathLength, float pivotOffset, float[] freeSegmentBounds)
    {
        var segments = new List<(float start, float end)>();
        for (int i = 0; i < freeSegmentBounds.Length; i += 2)
            segments.Add((freeSegmentBounds[i], freeSegmentBounds[i + 1]));
        return Path(pathLength, pivotOffset, segments.ToArray());
    }

    // A path with nothing blocked along it.
    private static SpawnPathData FreePath(float pathLength, float pivotOffset) => Path(pathLength, pivotOffset);

    // SpawnPathData's constructor reads a native Path, so the test sets the fields the placement reads; no segments is a path
    // free from end to end.
    private static SpawnPathData Path(float pathLength, float pivotOffset, params (float start, float end)[] freeSegments)
    {
        var path = (SpawnPathData)FormatterServices.GetUninitializedObject(typeof(SpawnPathData));
        var segments = new MBList<(float startOffset, float endOffset)>();
        foreach (var segment in freeSegments) segments.Add(segment);
        if (freeSegments.Length == 0) segments.Add((-pivotOffset, pathLength - pivotOffset));
        SetField(path, nameof(SpawnPathData.PathLength), pathLength);
        SetField(path, nameof(SpawnPathData.PivotOffset), pivotOffset);
        SetField(path, "_freeSegments", segments);
        return path;
    }

    // Sets a readonly SpawnPathData field the constructor would have.
    private static void SetField(SpawnPathData path, string name, object value) =>
        typeof(SpawnPathData).GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .SetValue(path, value);
}
#endif
