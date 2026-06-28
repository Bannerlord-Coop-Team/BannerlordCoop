using GameInterface.Services.MapEvents;
using System.Runtime.Serialization;
using TaleWorlds.CampaignSystem.MapEvents;
using Xunit;

namespace GameInterface.Tests.Services.MapEvents;

public class MapEventRegistryTests
{
    [Fact]
    public void InitializeClientMapEvent_InitializesConstructorBackedState()
    {
        var mapEvent = (MapEvent)FormatterServices.GetUninitializedObject(typeof(MapEvent));

        MapEventRegistry.InitializeClientMapEvent(mapEvent, "Created_42");

        Assert.Equal("Created_42", mapEvent.StringId);
        Assert.NotNull(mapEvent._sides);
        Assert.Equal(2, mapEvent._sides.Length);
        Assert.NotNull(mapEvent.WonRounds);
        Assert.NotNull(mapEvent.TroopUpgradeTracker);
    }
}
