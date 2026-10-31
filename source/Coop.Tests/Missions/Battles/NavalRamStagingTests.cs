#if COOP_NAVAL && DEBUG
using Missions.Naval;
using TaleWorlds.Library;
using Xunit;

namespace Coop.Tests.Missions.Battles;

/// <summary>Checks ram capsule clearance and moving-target interception without a mission or native physics.</summary>
public class NavalRamStagingTests
{
    /// <summary>A rotated, offset hull is approached at its real amidships with the capsule clear of its side.</summary>
    [Theory]
    [InlineData(0f)]
    [InlineData(1.5707963f)]
    [InlineData(3.1415927f)]
    public void Approach_PlacesActualTipOffBodyCentre(float heading)
    {
        var target = MatrixFrame.Identity;
        target.rotation.RotateAboutUp(heading);
        target.origin = new Vec3(200f, 300f, 20f);
        var tip = new Vec3(1.5f, 20f, -0.5f);
        var approach = new NavalRamStaging().Approach(target, new Vec3(-4f, -12f, -2f),
            new Vec3(8f, 18f, 2f), tip, 0.5f, Vec3.Zero);
        var actualTip = target.TransformToLocal(approach.Frame.TransformToParent(tip));
        Assert.Equal(14.5f, actualTip.x, 3);
        Assert.Equal(3f, actualTip.y, 3);
        Assert.Equal(0f, Vec3.DotProduct(approach.Frame.rotation.f, target.rotation.f), 3);
        Assert.Equal(20f, approach.Frame.origin.z);
        Assert.Equal(1f, approach.Frame.rotation.u.z, 3);
    }

    /// <summary>Target translation is inherited so the tip crosses the side centrally above vanilla's 3 m/s gate.</summary>
    [Theory]
    [InlineData(0f, 0f)]
    [InlineData(4f, 7f)]
    [InlineData(-6f, -9f)]
    public void Approach_ClosesOnMovingTargetWithoutAlongKeelDrift(float vx, float vy)
    {
        var target = MatrixFrame.Identity;
        target.origin = new Vec3(200f, 300f, 20f);
        var tip = new Vec3(1.5f, 20f, 0f);
        var targetVelocity = new Vec3(vx, vy, 2f);
        var approach = new NavalRamStaging().Approach(target, new Vec3(-4f, -12f, -2f),
            new Vec3(8f, 18f, 2f), tip, 0.5f, targetVelocity);
        var relative = approach.Velocity - new Vec3(vx, vy, 0f);
        Assert.Equal(10f, Vec3.DotProduct(relative, approach.Frame.rotation.f), 3);
        Assert.Equal(0f, Vec3.DotProduct(relative, target.rotation.f), 3);
        var contactTip = approach.Frame.TransformToParent(tip) + (relative * 0.6f);
        var localContact = target.TransformToLocal(contactTip);
        Assert.Equal(8.5f, localContact.x, 3);
        Assert.Equal(3f, localContact.y, 3);
        Assert.Equal(0f, approach.Velocity.z);
    }
}
#endif
