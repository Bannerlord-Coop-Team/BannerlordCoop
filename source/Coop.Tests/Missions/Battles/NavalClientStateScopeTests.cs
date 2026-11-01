#if COOP_NAVAL
using Autofac;
using Missions.Naval;
using NavalDLC.Missions.NavalPhysics;
using NavalDLC.Missions.Objects;
using Xunit;

namespace Coop.Tests.Missions.Battles;

/// <summary>
/// PR #4191 M1: the naval mission state patches read belongs to one client container, so E2E clients sharing a process
/// and a later mission dont see another client's values.
/// </summary>
[Collection("Mission.Current")]
public class NavalClientStateScopeTests
{
    // Each client container gets its own tracker, and a mission child scope shares its client's.
    [Fact]
    public void MissionTracker_IsOnePerClientContainer()
    {
        using var first = BuildClient();
        using var second = BuildClient();
        using var missionScope = first.BeginLifetimeScope("coop-mission");

        first.Resolve<INavalMissionTracker>().IsActive = true;

        Assert.False(second.Resolve<INavalMissionTracker>().IsActive);
        Assert.Same(first.Resolve<INavalMissionTracker>(), missionScope.Resolve<INavalMissionTracker>());
    }

    // A patch reads the battle state of the client container that is active, not of another client.
    [Fact]
    public void Patch_ReadsTheActiveClientsMissionState()
    {
        using var inBattle = BuildClient();
        using var idle = BuildClient();
        inBattle.Resolve<INavalMissionTracker>().IsActive = true;

        try
        {
            using (global::GameInterface.ContainerProvider.UseContainerThreadSafe(idle))
                Assert.True(CoopOrderOfBattleShipLayoutPatch.Prefix());

            using (global::GameInterface.ContainerProvider.UseContainerThreadSafe(inBattle))
                Assert.False(CoopOrderOfBattleShipLayoutPatch.Prefix());
        }
        finally
        {
            global::GameInterface.ContainerProvider.Clear();
        }
    }

    // Each client container keeps its own copy set, so one client's copies stay unknown to another.
    [Fact]
    public void ForeignHullRegistry_IsOnePerClientContainer()
    {
        using var first = BuildClient();
        using var second = BuildClient();
        var copy = ShipTestHulls.Uninitialized<MissionShip>();
        copy._physics = ShipTestHulls.Uninitialized<NavalPhysics>();

        first.Resolve<INavalForeignHullRegistry>().Add(copy, null);

        Assert.False(second.Resolve<INavalForeignHullRegistry>().Contains(copy));
        Assert.Same(first.Resolve<INavalForeignHullRegistry>(), first.BeginLifetimeScope().Resolve<INavalForeignHullRegistry>());
    }

    // A patch filters a joint force only on a hull that is a copy in the active client container.
    [Fact]
    public void Patch_ReadsTheActiveClientsCopies()
    {
        using var owner = BuildClient();
        using var copier = BuildClient();
        var physics = ShipTestHulls.Uninitialized<NavalPhysics>();
        var hull = ShipTestHulls.Uninitialized<MissionShip>();
        hull._physics = physics;
        owner.Resolve<INavalMissionTracker>().IsActive = true;
        copier.Resolve<INavalMissionTracker>().IsActive = true;
        copier.Resolve<INavalForeignHullRegistry>().Add(hull, null);

        try
        {
            using (global::GameInterface.ContainerProvider.UseContainerThreadSafe(owner))
                Assert.True(RopeForcePatch.AllowForce(physics, 0));

            using (global::GameInterface.ContainerProvider.UseContainerThreadSafe(copier))
                Assert.False(RopeForcePatch.AllowForce(physics, 0));
        }
        finally
        {
            global::GameInterface.ContainerProvider.Clear();
            NavalRopes.Clear();
        }
    }

    // A client container with the naval registrations.
    private static IContainer BuildClient()
    {
        var builder = new ContainerBuilder();
        builder.RegisterModule<NavalMissionModule>();
        return builder.Build();
    }
}
#endif
