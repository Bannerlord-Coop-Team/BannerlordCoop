using Missions.Battles;
using System.Reflection;
using Xunit;

namespace Coop.Tests.Missions.Battles;

public class SiegeWeaponFireReplicatorTests
{
    [Theory]
    [InlineData(true, false, true)]
    [InlineData(true, true, false)]
    [InlineData(false, false, false)]
    [InlineData(false, true, false)]
    public void NetworkGateHit_AppliesDamageOnlyOnTheHostForARemoteRam(
        bool isLocalHost,
        bool ramSimulatedLocally,
        bool expected)
    {
        var method = typeof(SiegeWeaponFireReplicator).GetMethod(
            "ShouldApplyHostGateDamage",
            BindingFlags.Static | BindingFlags.NonPublic);

        Assert.NotNull(method);
        bool result = Assert.IsType<bool>(method.Invoke(null, new object[] { isLocalHost, ramSimulatedLocally }));

        Assert.Equal(expected, result);
    }

    [Fact]
    public void SiegeMachineEvents_AcceptOnlyTheCurrentAuthorityTuple()
    {
        Assert.True(SiegeWeaponFireReplicator.IsCurrentMachineAuthority(
            "owner-b", 7, 3, "owner-b", 7, 3));
        Assert.False(SiegeWeaponFireReplicator.IsCurrentMachineAuthority(
            "owner-a", 7, 2, "owner-b", 7, 3));
        Assert.False(SiegeWeaponFireReplicator.IsCurrentMachineAuthority(
            "owner-b", 6, 3, "owner-b", 7, 3));
    }
}
