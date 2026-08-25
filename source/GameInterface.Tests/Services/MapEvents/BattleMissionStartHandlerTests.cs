using Common.Messaging;
using GameInterface.Services.MapEvents.Handlers;
using TaleWorlds.Core;
using Xunit;

namespace GameInterface.Tests.Services.MapEvents;

public class BattleMissionStartHandlerTests
{
    // The two missionOpen: true rows prove Clear wins regardless of battleStillValid, guarding
    // against a future reorder that would let a stale/invalid battle override an opened mission.
    [Theory]
    [InlineData(false, true, BattleMissionStartHandler.DeferredOpenAction.Keep)]
    [InlineData(false, false, BattleMissionStartHandler.DeferredOpenAction.Abandon)]
    [InlineData(true, false, BattleMissionStartHandler.DeferredOpenAction.Clear)]
    [InlineData(true, true, BattleMissionStartHandler.DeferredOpenAction.Clear)]
    public void DecideDeferredOpenAction_ResolvesByMissionAndBattleState(
        bool missionOpen, bool battleStillValid, object expected)
    {
        Assert.Equal((BattleMissionStartHandler.DeferredOpenAction)expected,
            BattleMissionStartHandler.DecideDeferredOpenAction(missionOpen, battleStillValid));
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
