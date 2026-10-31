#if COOP_NAVAL
using Autofac;
using Missions.Naval;
using Xunit;

namespace Coop.Tests.Missions.Battles;

/// <summary>
/// PR #4191 M1: the naval mission state patches read belongs to one client container, so E2E clients sharing a process
/// and a later mission dont see another client's values.
/// </summary>
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

    // A client container with the naval registrations.
    private static IContainer BuildClient()
    {
        var builder = new ContainerBuilder();
        builder.RegisterModule<NavalMissionModule>();
        return builder.Build();
    }
}
#endif
