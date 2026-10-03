using Common.Messaging;
using GameInterface.Services.MapEvents.Handlers;
using TaleWorlds.Core;
using Xunit;

namespace GameInterface.Tests.Services.MapEvents;

public class BattleMissionStartHandlerTests
{
    [Theory]
    [InlineData(false, false, true)]
    [InlineData(true, false, false)]
    [InlineData(true, true, true)]
    public void IsNavalBattleAdmitted_RequiresNavalDlcOnlyForNavalMapEvents(bool isNaval, bool navalDlcActive, bool expected)
    {
        Assert.Equal(expected, BattleMissionStartHandler.IsNavalBattleAdmitted(isNaval, navalDlcActive));
    }

    [Fact]
    public void GetOrCreateMissionInitializerSnapshot_ReusesFirstBattleInitializer()
    {
        using var messageBroker = new MessageBroker();
        using var handler = new BattleMissionStartHandler(
            messageBroker,
            null!,
            null!,
            null!,
            null!,
            null!,
            null!);

        var initial = new MissionInitializerRecord("battle_terrain_026")
        {
            RandomTerrainSeed = 1234,
        };
        var later = new MissionInitializerRecord("battle_terrain_030")
        {
            RandomTerrainSeed = 5678,
        };

        var first = handler.GetOrCreateMissionInitializerSnapshot("map-event-1", () => initial);
        var repeated = handler.GetOrCreateMissionInitializerSnapshot("map-event-1", () => later);
        var otherBattle = handler.GetOrCreateMissionInitializerSnapshot("map-event-2", () => later);

        Assert.Equal("battle_terrain_026", first.SceneName);
        Assert.Equal(first.SceneName, repeated.SceneName);
        Assert.Equal(1234, repeated.RandomTerrainSeed);
        Assert.Equal("battle_terrain_030", otherBattle.SceneName);
    }
}
