using GameInterface.Services.Locations;
using Xunit;

namespace Coop.Tests.Missions.Locations;

public class LocationNpcGateTests
{
    private readonly LocationNpcGateState gate = new LocationNpcGateState();

    [Fact]
    public void Inactive_NothingSuppressed()
    {
        Assert.False(gate.IsCoopLocationMissionActive);
        Assert.Null(gate.ActiveInstanceId);
        Assert.False(gate.IsLocalHostConfirmed);
        Assert.False(gate.ShouldSuppressNativeSpawns);
    }

    [Fact]
    [Trait("Requirement", "SR-013")]
    public void BeginMission_SuppressesUntilHostConfirmed()
    {
        gate.BeginMission("settlement1|loc_tavern");

        Assert.True(gate.IsCoopLocationMissionActive);
        Assert.Equal("settlement1|loc_tavern", gate.ActiveInstanceId);
        Assert.False(gate.IsLocalHostConfirmed);
        Assert.True(gate.ShouldSuppressNativeSpawns);
    }

    [Fact]
    [Trait("Requirement", "SR-011")]
    public void ConfirmedHost_LiftsSuppression()
    {
        gate.BeginMission("settlement1|loc_tavern");
        gate.SetLocalHost("settlement1|loc_tavern", isLocalHost: true);

        Assert.True(gate.IsLocalHostConfirmed);
        Assert.False(gate.ShouldSuppressNativeSpawns);
    }

    [Fact]
    [Trait("Requirement", "SR-012")]
    public void ConfirmedNonHost_StaysSuppressed()
    {
        gate.BeginMission("settlement1|loc_tavern");
        gate.SetLocalHost("settlement1|loc_tavern", isLocalHost: false);

        Assert.False(gate.IsLocalHostConfirmed);
        Assert.True(gate.ShouldSuppressNativeSpawns);
    }

    [Fact]
    public void SetLocalHost_ForAnotherInstance_IsIgnored()
    {
        gate.BeginMission("settlement1|loc_tavern");
        gate.SetLocalHost("settlement9|loc_center", isLocalHost: true);

        Assert.False(gate.IsLocalHostConfirmed);
        Assert.True(gate.ShouldSuppressNativeSpawns);
    }

    [Fact]
    public void SetLocalHost_WhileInactive_IsIgnored()
    {
        gate.SetLocalHost("settlement1|loc_tavern", isLocalHost: true);

        Assert.False(gate.IsLocalHostConfirmed);
        Assert.False(gate.ShouldSuppressNativeSpawns);
    }

    [Fact]
    public void EndMission_ResetsEverything()
    {
        gate.BeginMission("settlement1|loc_tavern");
        gate.SetLocalHost("settlement1|loc_tavern", isLocalHost: true);
        gate.SuppressCapture = true;

        gate.EndMission();

        Assert.False(gate.IsCoopLocationMissionActive);
        Assert.Null(gate.ActiveInstanceId);
        Assert.False(gate.IsLocalHostConfirmed);
        Assert.False(gate.ShouldSuppressNativeSpawns);
        Assert.False(gate.SuppressCapture);
    }

    [Fact]
    public void BeginMission_ResetsHostConfirmationOfThePreviousMission()
    {
        gate.BeginMission("settlement1|loc_tavern");
        gate.SetLocalHost("settlement1|loc_tavern", isLocalHost: true);
        gate.SuppressCapture = true;

        gate.BeginMission("settlement1|loc_center");

        Assert.False(gate.IsLocalHostConfirmed);
        Assert.True(gate.ShouldSuppressNativeSpawns);
        Assert.False(gate.SuppressCapture);
    }

    [Fact]
    public void SuppressCapture_RoundTrips()
    {
        Assert.False(gate.SuppressCapture);
        gate.SuppressCapture = true;
        Assert.True(gate.SuppressCapture);
        gate.SuppressCapture = false;
        Assert.False(gate.SuppressCapture);
    }
}
