using GameInterface.Services.MapEvents;
using System.Collections.Generic;
using Xunit;

namespace GameInterface.Tests.Services.MapEvents;

/// <summary>Covers the per-client siege authority: host-default vs claimant simulation, remote aims and reset.</summary>
public class SiegeMachineAuthorityRegistryTests
{
    private const int MachineId = 7;
    private const int OtherMachineId = 8;

    private readonly SiegeMachineAuthorityRegistry registry = new();

    /// <summary>The host simulates every machine except the ones a client claimed.</summary>
    [Fact]
    public void Host_SimulatesUnclaimedMachines_AndNotRemotelyClaimedOnes()
    {
        registry.IsLocalAuthority = true;
        registry.SetClaimedMachines(new HashSet<int>(), new HashSet<int> { MachineId });

        Assert.False(registry.IsMachineSimulatedLocally(MachineId));
        Assert.True(registry.IsMachineSimulatedLocally(OtherMachineId));
    }

    /// <summary>A non-host client simulates only the machines it claimed.</summary>
    [Fact]
    public void Client_SimulatesOnlyLocallyClaimedMachines()
    {
        registry.SetClaimedMachines(new HashSet<int> { MachineId }, new HashSet<int>());

        Assert.True(registry.IsMachineSimulatedLocally(MachineId));
        Assert.False(registry.IsMachineSimulatedLocally(OtherMachineId));
    }

    /// <summary>A received aim is readable only for a machine simulated elsewhere.</summary>
    [Fact]
    public void TryGetRemoteAim_ReturnsFalseForALocallySimulatedMachine()
    {
        registry.SetRemoteAim(MachineId, 0.75f, 0.25f);
        registry.SetClaimedMachines(new HashSet<int> { MachineId }, new HashSet<int>());

        Assert.False(registry.TryGetRemoteAim(MachineId, out float direction, out float releaseAngle));
        Assert.Equal(0f, direction);
        Assert.Equal(0f, releaseAngle);
    }

    /// <summary>A received aim for a machine simulated elsewhere is returned as stored.</summary>
    [Fact]
    public void TryGetRemoteAim_ReturnsTheStoredAimForARemotelySimulatedMachine()
    {
        registry.SetRemoteAim(MachineId, 0.75f, 0.25f);

        Assert.True(registry.TryGetRemoteAim(MachineId, out float direction, out float releaseAngle));
        Assert.Equal(0.75f, direction);
        Assert.Equal(0.25f, releaseAngle);
        Assert.False(registry.TryGetRemoteAim(OtherMachineId, out _, out _));
    }

    /// <summary>Reset forgets the authority flags, the claims and the received aims.</summary>
    [Fact]
    public void Reset_ClearsFlagsClaimsAndAims()
    {
        registry.IsLocalAuthority = true;
        registry.IsAuthorityKnown = true;
        registry.SetClaimedMachines(new HashSet<int>(), new HashSet<int> { MachineId });
        registry.SetRemoteAim(MachineId, 0.75f, 0.25f);

        registry.Reset();

        Assert.False(registry.IsLocalAuthority);
        Assert.False(registry.IsAuthorityKnown);
        Assert.False(registry.IsMachineSimulatedLocally(MachineId));
        Assert.False(registry.TryGetRemoteAim(MachineId, out _, out _));

        registry.IsLocalAuthority = true;
        Assert.True(registry.IsMachineSimulatedLocally(MachineId));
    }
}
