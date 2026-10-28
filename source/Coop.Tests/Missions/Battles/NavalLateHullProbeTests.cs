#if COOP_NAVAL
using Missions.Naval;
using Moq;
using NavalDLC.Missions.Objects;
using System;
using TaleWorlds.MountAndBlade;
using Xunit;

namespace Coop.Tests.Missions.Battles;

/// <summary>Checks late-hull diagnostics read the ship target wrappers that vanilla caches.</summary>
public class NavalLateHullProbeTests
{
    /// <summary>Two target objects for one hull must report one network identity, including the selected target.</summary>
    [Fact]
    public void TargetHullIds_ResolvesAndDeduplicatesVanillaTargetWrappers()
    {
        var hull = ShipTestHulls.Uninitialized<MissionShip>();
        var first = ShipTestHulls.Uninitialized<ShipTargetMissionObject>();
        var second = ShipTestHulls.Uninitialized<ShipTargetMissionObject>();
        first._ship = hull;
        second._ship = hull;
        var id = Guid.NewGuid();
        var hulls = new Mock<INavalDebugHulls>(MockBehavior.Strict);
        hulls.Setup(value => value.ShipIdOf(hull)).Returns(id);
        var probe = new NavalLateHullProbe(hulls.Object);

        Assert.Equal(id, Assert.Single(probe.TargetHullIds(new ITargetable[] { first, second })));
        Assert.Equal(id, probe.TargetHullId(first));
    }

    /// <summary>A missing initialization must remain distinguishable from a populated cache with no ship targets.</summary>
    [Fact]
    public void TargetHullIds_DistinguishesUninitializedAndEmptyCaches()
    {
        var probe = new NavalLateHullProbe(Mock.Of<INavalDebugHulls>());

        Assert.Null(probe.TargetHullIds(null!));
        Assert.Empty(probe.TargetHullIds(Array.Empty<ITargetable>()));
        Assert.Null(probe.TargetHullId(null!));
    }
}
#endif
