using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;
using TaleWorlds.ObjectSystem;

namespace Missions.Naval;

/// <summary>
/// The spawn path slot math for a side's fleets: each player's by rank and the allied AI fleet's a gap ahead of the front-most
/// player, which every client derives alike. Stateless; the own rank lives in <see cref="INavalPlayerDeploymentSlotTracker"/>.
/// </summary>
internal static class NavalSideDeploymentSlots
{
    // DefaultNavalMissionLogic.MakeDeploymentPlansForSide's gap between a side's teams.
    internal const float InterTeamGap = 32f;

    // DefaultNavalMissionLogic.GetTeamSpawnPathOffsetRange's margin on a team's longest ship deployment length.
    internal const float TeamRangeMargin = 1.1f;

    // NavalShipsLogic.ComputeSpawnPathDeploymentOffset's distance between the two sides for a battle of two ships or fewer.
    internal const float SmallBattleSideDistance = 400f;

    /// <summary>How many player parties precede <paramref name="own"/> in the side's party order.</summary>
    internal static int RankOf<T>(IEnumerable<T> sideParties, T own, Func<T, bool> isPlayerParty) where T : class
    {
        int rank = 0;
        foreach (var party in sideParties)
        {
            if (ReferenceEquals(party, own)) return rank;
            if (isPlayerParty(party)) rank++;
        }

        return 0;
    }

    /// <summary>How many of the side's parties are player parties.</summary>
    internal static int PlayerCountOf<T>(IEnumerable<T> sideParties, Func<T, bool> isPlayerParty) =>
        sideParties.Count(isPlayerParty);

    /// <summary>A team's range from its ships' deployment lengths, as vanilla's GetTeamSpawnPathOffsetRange takes it.</summary>
    internal static float LongestTeamRange(IEnumerable<float> shipDeploymentLengths) =>
        TeamRangeMargin * shipDeploymentLengths.DefaultIfEmpty(0f).Max();

    /// <summary>[Game thread] The deepest team range any fleet can have, from every ship's deployment length in the game data.</summary>
    internal static float LongestTeamRangeOfGame() =>
        LongestTeamRange(MBObjectManager.Instance.GetObjectTypeList<MissionShipObject>().Select(ship => ship.DeploymentArea.y));

    /// <summary>
    /// Vanilla's side deployment offset for a battle of two ships or fewer. Vanilla deepens it with the ships it assigned, and in
    /// coop each client assigns only its own fleet, so a side with several players takes this value every client shares.
    /// </summary>
    internal static float SharedDeploymentOffset(float pathLength) => (-Math.Min(pathLength, SmallBattleSideDistance) / 2f) + 1f;

    /// <summary>
    /// The side's first <paramref name="ranks"/> player slots (at least one) from vanilla's base for the shared offset, each fleet
    /// as deep as <paramref name="range"/>, and the opposing base they face.
    /// </summary>
    internal static NavalSideLayout Layout(SpawnPathData spawnPath, int ranks, float range)
    {
        var (sideBase, opposingBase, forwardLimit) = Bounds(spawnPath, range);
        var slots = new NavalSideSlot[ranks];
        slots[0] = FrontSlot(spawnPath, sideBase, range, forwardLimit);

        int rank = 1;
        float behindCursor = slots[0].Offset - range - InterTeamGap;
        for (; rank < ranks && TryPlaceBehind(spawnPath, behindCursor, range, out float behind); rank++)
        {
            slots[rank] = new NavalSideSlot(behind, NavalSlotProblem.None);
            behindCursor = behind - range - InterTeamGap;
        }

        float aheadRear = slots[0].Offset + InterTeamGap;
        for (; rank < ranks; rank++)
        {
            slots[rank] = SlotAhead(spawnPath, aheadRear, range, forwardLimit);
            aheadRear = slots[rank].Offset + InterTeamGap;
        }

        return new NavalSideLayout(slots, opposingBase);
    }

    /// <summary>
    /// The allied AI fleet's slot and the opposing base it faces: a gap ahead of the front-most of the side's
    /// <paramref name="playerCount"/> player fleets, with rank 0 on the host's own plan when it is rank 0 (a lone player's is
    /// vanilla's). Vanilla keeps the allies in front even when its forward packing puts every team ahead of the base.
    /// </summary>
    internal static (NavalSideSlot Slot, float OpposingBase) AllySlot(SpawnPathData spawnPath, int ownRank, float ownPlanOffset,
        int playerCount, float allyRange, float gameRange)
    {
        var (_, opposingBase, forwardLimit) = Bounds(spawnPath, gameRange);
        var slots = Layout(spawnPath, Math.Max(1, playerCount), gameRange).Slots;
        float rankZero = ownRank == 0 ? ownPlanOffset : slots[0].Offset;
        float frontMost = new[] { rankZero }.Concat(slots.Skip(1).Select(slot => slot.Offset)).Max();

        return (SlotAhead(spawnPath, frontMost + InterTeamGap, allyRange, forwardLimit), opposingBase);
    }

    // Vanilla's base and opposing base for the shared offset, and the offset no fleet as deep as the range goes past.
    private static (float SideBase, float OpposingBase, float ForwardLimit) Bounds(SpawnPathData spawnPath, float range)
    {
        DefaultBattleMissionAgentSpawnLogic.ComputeDeploymentBaseOffsets(spawnPath, SharedDeploymentOffset(spawnPath.PathLength),
            out float sideBase, out float opposingBase);
        return (sideBase, opposingBase, opposingBase - range - InterTeamGap);
    }

    // Rank 0 takes vanilla's slot behind the base, or the first room from the path start when the base leaves none behind.
    private static NavalSideSlot FrontSlot(SpawnPathData spawnPath, float sideBase, float range, float forwardLimit)
    {
        if (TryPlaceBehind(spawnPath, sideBase, range, out float behind)) return new NavalSideSlot(behind, NavalSlotProblem.None);

        return SlotAhead(spawnPath, -spawnPath.PivotOffset, range, forwardLimit);
    }

    // The first room past the rear, held short of the forward limit; the slot names what went wrong.
    private static NavalSideSlot SlotAhead(SpawnPathData spawnPath, float rear, float range, float forwardLimit)
    {
        if (!TryPlaceAhead(spawnPath, rear, range, out float ahead))
            return new NavalSideSlot(Math.Min(rear + range, forwardLimit), NavalSlotProblem.NoRoomOnPath);
        if (ahead > forwardLimit) return new NavalSideSlot(forwardLimit, NavalSlotProblem.HeldShortOfOpposingBase);

        return new NavalSideSlot(ahead, NavalSlotProblem.None);
    }

    // Vanilla's search for one team at a time, so a slot depends only on the slots before it and never on how many ranks a
    // client counts, which vanilla's packing of every team at once would (R49 C3).
    private static bool TryPlaceBehind(SpawnPathData spawnPath, float cursor, float range, out float offset) =>
        TryPlaceOne(spawnPath, cursor, range, searchBackwardFromBase: true, out offset);

    // Vanilla's forward search for one team from the rear, see TryPlaceBehind.
    private static bool TryPlaceAhead(SpawnPathData spawnPath, float rear, float range, out float offset) =>
        TryPlaceOne(spawnPath, rear, range, searchBackwardFromBase: false, out offset);

    // One team through DefaultBattleMissionAgentSpawnLogic.TryPlaceTeamsOneSide.
    private static bool TryPlaceOne(SpawnPathData spawnPath, float from, float range, bool searchBackwardFromBase, out float offset)
    {
        var offsets = new float[1];
        bool placed = DefaultBattleMissionAgentSpawnLogic.TryPlaceTeamsOneSide(spawnPath, from, new[] { range }, InterTeamGap,
            searchBackwardFromBase, ref offsets);
        offset = offsets[0];
        return placed;
    }
}
