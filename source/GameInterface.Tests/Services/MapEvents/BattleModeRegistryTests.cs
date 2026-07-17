using GameInterface.Services.MapEvents;
using GameInterface.Services.MapEvents.Handlers;
using Xunit;

namespace GameInterface.Tests.Services.MapEvents;

/// <summary>Tests map-event-scoped client battle mode tracking.</summary>
public class BattleModeRegistryTests
{
    [Fact]
    public void End_DifferentMapEvent_DoesNotClearCurrentMode()
    {
        BattleModeRegistry.Begin("current-map-event", BattleStartMode.Mission);

        try
        {
            Assert.False(BattleModeRegistry.End("old-map-event"));

            Assert.True(BattleModeRegistry.IsMission("current-map-event"));
        }
        finally
        {
            BattleModeRegistry.End();
        }
    }

    [Fact]
    public void End_CurrentMapEvent_ClearsModeAndReportsChange()
    {
        BattleModeRegistry.Begin("current-map-event", BattleStartMode.Mission);

        Assert.True(BattleModeRegistry.End("current-map-event"));
        Assert.False(BattleModeRegistry.IsMission("current-map-event"));
    }

    // BR-104: the client's mode record is scoped to the battle instance id, so a query keyed by a previous or
    // unrelated battle's map-event id never observes the current mission's mode — a message from a different
    // battle cannot be mistaken for the current one.
    [Fact]
    [Trait("Requirement", "BR-104")]
    public void IsMission_ForForeignMapEventId_DoesNotSeeTheCurrentMission()
    {
        BattleModeRegistry.Begin("current-map-event", BattleStartMode.Mission);

        try
        {
            Assert.True(BattleModeRegistry.IsMission("current-map-event"));
            Assert.False(BattleModeRegistry.IsMission("previous-map-event"));
            Assert.False(BattleModeRegistry.IsSimulation("previous-map-event"));
        }
        finally
        {
            BattleModeRegistry.End();
        }
    }
}
