using Common.Messaging;
using GameInterface.Services.MapEvents.Handlers;
using TaleWorlds.Library;
using Xunit;

namespace GameInterface.Tests.Services.MapEvents;

public class BattleMissionStartHandlerTests
{
    [Fact]
    public void GetOrCreateAtmosphereSnapshot_ReusesFirstBattleAtmosphere()
    {
        using var messageBroker = new MessageBroker();
        using var handler = new BattleMissionStartHandler(
            messageBroker,
            null!,
            null!,
            null!,
            null!,
            null!);

        var initial = new AtmosphereInfo
        {
            TimeInfo = new TimeInformation { TimeOfDay = 6f },
        };
        var later = new AtmosphereInfo
        {
            TimeInfo = new TimeInformation { TimeOfDay = 18f },
        };

        var first = handler.GetOrCreateAtmosphereSnapshot("map-event-1", () => initial);
        var repeated = handler.GetOrCreateAtmosphereSnapshot("map-event-1", () => later);
        var otherBattle = handler.GetOrCreateAtmosphereSnapshot("map-event-2", () => later);

        Assert.Equal(6f, first.TimeInfo.TimeOfDay);
        Assert.Equal(first.TimeInfo.TimeOfDay, repeated.TimeInfo.TimeOfDay);
        Assert.Equal(18f, otherBattle.TimeInfo.TimeOfDay);
    }
}
