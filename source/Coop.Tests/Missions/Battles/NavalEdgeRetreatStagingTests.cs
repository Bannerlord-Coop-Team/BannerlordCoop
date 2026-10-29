#if COOP_NAVAL && DEBUG
using Missions.Naval;
using TaleWorlds.Library;
using Xunit;

namespace Coop.Tests.Missions.Battles;

/// <summary>Checks edge staging stays inside the vanilla removal radius and faces outward without native physics.</summary>
public class NavalEdgeRetreatStagingTests
{
    /// <summary>Axis-aligned and diagonal approaches preserve water height and enter even the minimum retreat radius.</summary>
    [Theory]
    [InlineData(20f, 0f)]
    [InlineData(-20f, 0f)]
    [InlineData(0f, 20f)]
    [InlineData(0f, -20f)]
    [InlineData(30f, 40f)]
    public void AtEdge_StagesInsideFacingBoundary(float dx, float dy)
    {
        var origin = new Vec3(200f, 300f, 17f);
        var boundary = origin.AsVec2 + new Vec2(dx, dy);
        var frame = new NavalEdgeRetreatStaging().AtEdge(origin, boundary);
        var toBoundary = boundary - frame.origin.AsVec2;
        var outward = (boundary - origin.AsVec2).Normalized();

        Assert.Equal(0.25f, toBoundary.Length, 3);
        Assert.True(toBoundary.LengthSquared < 0.5f * 0.5f);
        Assert.Equal(1f, Vec2.DotProduct(toBoundary.Normalized(), outward), 3);
        Assert.Equal(1f, Vec2.DotProduct(frame.rotation.f.AsVec2, outward), 3);
        Assert.Equal(17f, frame.origin.z);
        Assert.Equal(1f, frame.rotation.u.z, 3);
    }
}
#endif
