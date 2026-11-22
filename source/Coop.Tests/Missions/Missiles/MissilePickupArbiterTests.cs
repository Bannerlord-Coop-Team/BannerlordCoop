using Missions.Agents.Messages;
using Missions.Agents.Packets;
using Missions.Missiles;
using System;
using TaleWorlds.Core;
using Xunit;

namespace Coop.Tests.Missions.Missiles;

public class MissilePickupArbiterTests
{
    private static readonly Guid WorldItemId = Guid.NewGuid();
    private static readonly Guid FirstRanked = new("00000000-0000-0000-0000-000000000001");
    private static readonly Guid SecondRanked = new("00000000-0000-0000-0000-000000000002");

    [Fact]
    public void BetterRankedPickup_ArrivingFirst_WinsAndTheLaterOneLoses()
    {
        var arbiter = new MissilePickupArbiter();

        Assert.True(arbiter.TryClaim(Pickup(FirstRanked), out NetworkWeaponPickedup displaced));
        Assert.Null(displaced);
        Assert.False(arbiter.TryClaim(Pickup(SecondRanked), out displaced));
        Assert.Null(displaced);
    }

    [Fact]
    public void BetterRankedPickup_ArrivingSecond_DisplacesTheEarlierWinner()
    {
        var arbiter = new MissilePickupArbiter();
        NetworkWeaponPickedup earlier = Pickup(SecondRanked);

        Assert.True(arbiter.TryClaim(earlier, out _));
        Assert.True(arbiter.TryClaim(Pickup(FirstRanked), out NetworkWeaponPickedup displaced));
        Assert.Same(earlier, displaced);
        Assert.False(arbiter.TryClaim(earlier, out _));
    }

    [Fact]
    public void ReofferedWinner_StaysTheWinnerWithoutDisplacingItself()
    {
        var arbiter = new MissilePickupArbiter();
        NetworkWeaponPickedup winner = Pickup(FirstRanked);

        Assert.True(arbiter.TryClaim(winner, out _));
        Assert.True(arbiter.TryClaim(winner, out NetworkWeaponPickedup displaced));
        Assert.Null(displaced);
    }

    [Fact]
    public void PickupsOfDifferentMissiles_DoNotContest()
    {
        var arbiter = new MissilePickupArbiter();

        Assert.True(arbiter.TryClaim(Pickup(FirstRanked), out _));
        Assert.True(arbiter.TryClaim(Pickup(SecondRanked, Guid.NewGuid()), out NetworkWeaponPickedup displaced));
        Assert.Null(displaced);
    }

    [Fact]
    public void LateLosingPickup_AfterManyOtherMissilesWereClaimed_StillLoses()
    {
        var arbiter = new MissilePickupArbiter();

        Assert.True(arbiter.TryClaim(Pickup(FirstRanked), out _));
        for (int i = 0; i < 5000; i++)
            Assert.True(arbiter.TryClaim(Pickup(Guid.NewGuid(), Guid.NewGuid()), out _));

        Assert.False(arbiter.TryClaim(Pickup(SecondRanked), out NetworkWeaponPickedup displaced));
        Assert.Null(displaced);
    }

    // Builds a pickup that consumed a stuck missile item under the given pickup id.
    private static NetworkWeaponPickedup Pickup(Guid pickupId, Guid? worldItemId = null) =>
        new(Guid.NewGuid(), EquipmentIndex.Weapon0, worldItemId ?? WorldItemId, "arrows", null, null,
            default(AgentEquipmentData), 0, 1, 1, 0, true, pickupId: pickupId, isStuckMissile: true);
}
