using Missions.Battles;
using Xunit;

namespace E2E.Tests.Services.Missions;

/// <summary>
/// Unit tests (game-independent) for <see cref="CoopBattleMissionSpawnHandler.ResolvePendingSide"/> — the
/// per-tick decision that grows a coop battle side once its reserve lands after AfterStart sized it to zero.
/// Covers the reserve not yet arrived, an owned reserve arriving late, and an empty non-owned side.
/// </summary>
public class CoopBattleMissionSpawnHandlerSizingTests
{
    [Fact]
    public void ReserveNotArrived_StaysPending_NoResize()
    {
        var (stillPending, resize, _) = CoopBattleMissionSpawnHandler.ResolvePendingSide(populated: false, ownedTotal: 0);

        Assert.True(stillPending);
        Assert.False(resize);
    }

    [Fact]
    public void OwnedReserveArrivedLate_Settles_AndResizesToOwnedTotal()
    {
        var (stillPending, resize, newTotal) = CoopBattleMissionSpawnHandler.ResolvePendingSide(populated: true, ownedTotal: 7);

        Assert.False(stillPending);
        Assert.True(resize);
        Assert.Equal(7, newTotal);
    }

    [Fact]
    public void EmptyNonOwnedSide_Settles_WithoutResize()
    {
        // A side this client owns nothing on gets an empty reserve; it settles at zero and its troops arrive as puppets.
        var (stillPending, resize, _) = CoopBattleMissionSpawnHandler.ResolvePendingSide(populated: true, ownedTotal: 0);

        Assert.False(stillPending);
        Assert.False(resize);
    }

    [Fact]
    public void NotPopulated_IgnoresTransientTotal_StaysPending()
    {
        // populated gates everything: a count seen before the supplier flips populated is not acted on.
        var (stillPending, resize, _) = CoopBattleMissionSpawnHandler.ResolvePendingSide(populated: false, ownedTotal: 5);

        Assert.True(stillPending);
        Assert.False(resize);
    }
}
