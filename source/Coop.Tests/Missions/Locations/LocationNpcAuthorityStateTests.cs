using GameInterface.Services.Locations;
using Xunit;

namespace Coop.Tests.Missions.Locations;

public class LocationNpcAuthorityStateTests
{
    private readonly LocationNpcAuthorityState state = new LocationNpcAuthorityState();

    [Fact]
    public void Inactive_NothingSuppressed()
    {
        Assert.False(state.IsCoopLocationMissionActive);
        Assert.Null(state.ActiveInstanceId);
        Assert.False(state.IsLocalHostConfirmed);
        Assert.False(state.ShouldSuppressNativeSpawns);
    }

    [Fact]
    [Trait("Requirement", "SR-013")]
    public void BeginMission_SuppressesUntilHostConfirmed()
    {
        state.BeginMission("settlement1|loc_tavern");

        Assert.True(state.IsCoopLocationMissionActive);
        Assert.Equal("settlement1|loc_tavern", state.ActiveInstanceId);
        Assert.False(state.IsLocalHostConfirmed);
        Assert.True(state.ShouldSuppressNativeSpawns);
    }

    [Fact]
    [Trait("Requirement", "SR-011")]
    public void ConfirmedHost_LiftsSuppression()
    {
        state.BeginMission("settlement1|loc_tavern");
        state.SetLocalHost("settlement1|loc_tavern", isLocalHost: true);

        Assert.True(state.IsLocalHostConfirmed);
        Assert.False(state.ShouldSuppressNativeSpawns);
    }

    [Fact]
    [Trait("Requirement", "SR-012")]
    public void ConfirmedNonHost_StaysSuppressed()
    {
        state.BeginMission("settlement1|loc_tavern");
        state.SetLocalHost("settlement1|loc_tavern", isLocalHost: false);

        Assert.False(state.IsLocalHostConfirmed);
        Assert.True(state.ShouldSuppressNativeSpawns);
    }

    [Fact]
    public void SetLocalHost_ForAnotherInstance_IsIgnored()
    {
        state.BeginMission("settlement1|loc_tavern");
        state.SetLocalHost("settlement9|loc_center", isLocalHost: true);

        Assert.False(state.IsLocalHostConfirmed);
        Assert.True(state.ShouldSuppressNativeSpawns);
    }

    [Fact]
    public void SetLocalHost_WhileInactive_IsIgnored()
    {
        state.SetLocalHost("settlement1|loc_tavern", isLocalHost: true);

        Assert.False(state.IsLocalHostConfirmed);
        Assert.False(state.ShouldSuppressNativeSpawns);
    }

    [Fact]
    public void EndMission_ResetsEverything()
    {
        state.BeginMission("settlement1|loc_tavern");
        state.SetLocalHost("settlement1|loc_tavern", isLocalHost: true);
        state.SuppressCapture = true;

        state.EndMission();

        Assert.False(state.IsCoopLocationMissionActive);
        Assert.Null(state.ActiveInstanceId);
        Assert.False(state.IsLocalHostConfirmed);
        Assert.False(state.ShouldSuppressNativeSpawns);
        Assert.False(state.SuppressCapture);
    }

    [Fact]
    public void BeginMission_ResetsHostConfirmationOfThePreviousMission()
    {
        state.BeginMission("settlement1|loc_tavern");
        state.SetLocalHost("settlement1|loc_tavern", isLocalHost: true);
        state.SuppressCapture = true;

        state.BeginMission("settlement1|loc_center");

        Assert.False(state.IsLocalHostConfirmed);
        Assert.True(state.ShouldSuppressNativeSpawns);
        Assert.False(state.SuppressCapture);
    }

    [Fact]
    public void SuppressCapture_RoundTrips()
    {
        Assert.False(state.SuppressCapture);
        state.SuppressCapture = true;
        Assert.True(state.SuppressCapture);
        state.SuppressCapture = false;
        Assert.False(state.SuppressCapture);
    }
}
