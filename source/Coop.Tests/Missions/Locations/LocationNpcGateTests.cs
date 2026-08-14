using GameInterface.Services.Locations;
using System;
using Xunit;

namespace Coop.Tests.Missions.Locations;

/// <summary>
/// Truth table for the process-global <see cref="LocationNpcGate"/>. The gate is static state, so every
/// test resets it (see <see cref="Dispose"/>) — no other suite touches it concurrently.
/// </summary>
public class LocationNpcGateTests : IDisposable
{
    public LocationNpcGateTests()
    {
        LocationNpcGate.EndMission();
    }

    public void Dispose()
    {
        LocationNpcGate.EndMission();
        LocationNpcGate.SuppressCapture = false;
    }

    [Fact]
    public void Inactive_NothingSuppressed()
    {
        Assert.False(LocationNpcGate.IsCoopLocationMissionActive);
        Assert.Null(LocationNpcGate.ActiveInstanceId);
        Assert.False(LocationNpcGate.IsLocalHostConfirmed);
        Assert.False(LocationNpcGate.ShouldSuppressNativeSpawns);
    }

    [Fact]
    [Trait("Requirement", "SR-013")]
    public void BeginMission_SuppressesUntilHostConfirmed()
    {
        LocationNpcGate.BeginMission("settlement1|loc_tavern");

        Assert.True(LocationNpcGate.IsCoopLocationMissionActive);
        Assert.Equal("settlement1|loc_tavern", LocationNpcGate.ActiveInstanceId);
        Assert.False(LocationNpcGate.IsLocalHostConfirmed);
        Assert.True(LocationNpcGate.ShouldSuppressNativeSpawns);
    }

    [Fact]
    [Trait("Requirement", "SR-011")]
    public void ConfirmedHost_LiftsSuppression()
    {
        LocationNpcGate.BeginMission("settlement1|loc_tavern");
        LocationNpcGate.SetLocalHost("settlement1|loc_tavern", isLocalHost: true);

        Assert.True(LocationNpcGate.IsLocalHostConfirmed);
        Assert.False(LocationNpcGate.ShouldSuppressNativeSpawns);
    }

    [Fact]
    [Trait("Requirement", "SR-012")]
    public void ConfirmedNonHost_StaysSuppressed()
    {
        LocationNpcGate.BeginMission("settlement1|loc_tavern");
        LocationNpcGate.SetLocalHost("settlement1|loc_tavern", isLocalHost: false);

        Assert.False(LocationNpcGate.IsLocalHostConfirmed);
        Assert.True(LocationNpcGate.ShouldSuppressNativeSpawns);
    }

    [Fact]
    public void SetLocalHost_ForAnotherInstance_IsIgnored()
    {
        LocationNpcGate.BeginMission("settlement1|loc_tavern");

        // A stale assignment from a previous settlement visit must not flip the gate of the newer mission.
        LocationNpcGate.SetLocalHost("settlement9|loc_center", isLocalHost: true);

        Assert.False(LocationNpcGate.IsLocalHostConfirmed);
        Assert.True(LocationNpcGate.ShouldSuppressNativeSpawns);
    }

    [Fact]
    public void SetLocalHost_WhileInactive_IsIgnored()
    {
        LocationNpcGate.SetLocalHost("settlement1|loc_tavern", isLocalHost: true);

        Assert.False(LocationNpcGate.IsLocalHostConfirmed);
        Assert.False(LocationNpcGate.ShouldSuppressNativeSpawns);
    }

    [Fact]
    public void EndMission_ResetsEverything()
    {
        LocationNpcGate.BeginMission("settlement1|loc_tavern");
        LocationNpcGate.SetLocalHost("settlement1|loc_tavern", isLocalHost: true);

        LocationNpcGate.EndMission();

        Assert.False(LocationNpcGate.IsCoopLocationMissionActive);
        Assert.Null(LocationNpcGate.ActiveInstanceId);
        Assert.False(LocationNpcGate.IsLocalHostConfirmed);
        Assert.False(LocationNpcGate.ShouldSuppressNativeSpawns);
    }

    [Fact]
    public void BeginMission_ResetsHostConfirmationOfThePreviousMission()
    {
        LocationNpcGate.BeginMission("settlement1|loc_tavern");
        LocationNpcGate.SetLocalHost("settlement1|loc_tavern", isLocalHost: true);

        // A new mission (e.g. tavern -> town centre) starts unconfirmed even without an EndMission between.
        LocationNpcGate.BeginMission("settlement1|loc_center");

        Assert.False(LocationNpcGate.IsLocalHostConfirmed);
        Assert.True(LocationNpcGate.ShouldSuppressNativeSpawns);
    }

    [Fact]
    public void SuppressCapture_RoundTrips()
    {
        Assert.False(LocationNpcGate.SuppressCapture);
        LocationNpcGate.SuppressCapture = true;
        Assert.True(LocationNpcGate.SuppressCapture);
        LocationNpcGate.SuppressCapture = false;
        Assert.False(LocationNpcGate.SuppressCapture);
    }
}
