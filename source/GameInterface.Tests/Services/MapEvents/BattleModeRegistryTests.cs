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
            BattleModeRegistry.End("old-map-event");

            Assert.True(BattleModeRegistry.IsMission("current-map-event"));
        }
        finally
        {
            BattleModeRegistry.End();
        }
    }
}
