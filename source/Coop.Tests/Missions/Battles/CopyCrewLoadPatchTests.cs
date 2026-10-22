#if DEBUG
using Missions.Messages;
using Missions.Naval;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using Xunit;

namespace Coop.Tests.Missions.Battles;

public class CopyCrewLoadPatchTests
{
    private static readonly BattleShipCrewLoad OwnerLoad = BattleShipCrewLoad.Create(480f, new Vec3(0.5f, -4f, 1.5f));

    // R45 roll: only a copy whose owner sent a load takes it; a copy before its first sample and an own hull run vanilla.
    [Fact]
    public void AppliesOwnerLoad_OnlyOnACopyWithItsOwnersLoad()
    {
        Assert.True(CopyCrewLoadPatch.AppliesOwnerLoad(isCopy: true, OwnerLoad));
        Assert.False(CopyCrewLoadPatch.AppliesOwnerLoad(isCopy: true, null));
        Assert.False(CopyCrewLoadPatch.AppliesOwnerLoad(isCopy: false, OwnerLoad));
        Assert.False(CopyCrewLoadPatch.AppliesOwnerLoad(isCopy: false, null));
    }

    // Vanilla NavalPhysics.ApplyAgentForces: _committedTotalMass * StepAgentWeightMultiplier * MBGlobals.GravitationalAcceleration,
    // with MissionShip's multiplier 2 and gravity (0, 0, -9.806): six 80 kg agents weigh 9413.76 N.
    [Fact]
    public void WeightForce_IsVanillasCrewWeight()
    {
        var force = CopyCrewLoadPatch.WeightForce(480f, 2f);

        Assert.Equal(0f, force.x);
        Assert.Equal(0f, force.y);
        Assert.Equal(-9413.76f, force.z, 2);
    }

    // Vanilla skips the force when the crew centre lies outside the hull's physics box by more than 0.1 m.
    [Theory]
    [InlineData(0.5f, -4f, 1.5f, true)]
    [InlineData(3.05f, 0f, 0f, true)]
    [InlineData(3.2f, 0f, 0f, false)]
    [InlineData(0f, -12.5f, 0f, false)]
    [InlineData(0f, 0f, 6f, false)]
    public void IsInsideHull_IsVanillasBoxCheck(float x, float y, float z, bool expected)
    {
        var box = new BoundingBox { min = new Vec3(-3f, -12f, -1f), max = new Vec3(3f, 12f, 4f) };

        Assert.Equal(expected, CopyCrewLoadPatch.IsInsideHull(box, new Vec3(x, y, z)));
    }
}
#endif
