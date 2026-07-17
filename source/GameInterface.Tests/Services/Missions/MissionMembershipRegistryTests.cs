using Coop.Tests.Mocks;
using GameInterface.Services.Missions;
using System.Runtime.CompilerServices;
using Xunit;

namespace GameInterface.Tests.Services.Missions;

/// <summary>
/// Tests authoritative mission membership entry and release semantics.
/// </summary>
public class MissionMembershipRegistryTests
{
    static MissionMembershipRegistryTests()
    {
        RuntimeHelpers.RunModuleConstructor(typeof(TestNetwork).Module.ModuleHandle);
    }

    [Fact]
    public void MatchingLeave_RemovesEnteredController()
    {
        var registry = new MissionMembershipRegistry();
        var peer = new TestNetwork().CreatePeer();

        registry.Enter("battle-1", "controller", peer);
        Assert.True(registry.IsControllerInMission("controller"));

        registry.Leave("battle-1", "controller", peer);
        Assert.False(registry.IsControllerInMission("controller"));
    }

    [Fact]
    public void StaleLeave_DoesNotRemoveControllerFromNewMission()
    {
        var registry = new MissionMembershipRegistry();
        var network = new TestNetwork();
        var oldPeer = network.CreatePeer();
        var newPeer = network.CreatePeer();

        registry.Enter("battle-1", "controller", oldPeer);
        registry.Enter("battle-2", "controller", newPeer);
        registry.Leave("battle-1", "controller", oldPeer);

        Assert.True(registry.IsControllerInMission("controller"));
    }

    [Fact]
    public void ReplacedPeer_StaleLeaveDoesNotRemoveControllerFromSameMission()
    {
        var registry = new MissionMembershipRegistry();
        var network = new TestNetwork();
        var oldPeer = network.CreatePeer();
        var newPeer = network.CreatePeer();

        registry.Enter("battle-1", "controller", oldPeer);
        registry.Enter("battle-1", "controller", newPeer);
        registry.Leave("battle-1", "controller", oldPeer);

        Assert.True(registry.IsControllerInMission("controller"));

        registry.Leave("battle-1", "controller", newPeer);
        Assert.False(registry.IsControllerInMission("controller"));
    }
}
