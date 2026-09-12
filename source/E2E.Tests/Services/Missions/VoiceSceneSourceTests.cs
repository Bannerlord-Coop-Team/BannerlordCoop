using GameInterface.Services.Entity;
using GameInterface.Services.MapEvents;
using Missions.Battles;
using Missions.Services.Voice;
using Missions.Tournaments;
using Moq;
using Xunit;

namespace E2E.Tests.Services.Missions;

public class VoiceSceneSourceTests
{
    [Fact]
    public void UsesExistingBattleLocationAndTournamentInstanceIdentities()
    {
        var source = new VoiceSceneSource();
        var controller = Mock.Of<IControllerIdProvider>();
        var battle = new BattleSession(controller, Mock.Of<IBattleHostRegistry>());
        battle.TryBegin("map-event-7");
        Assert.Equal("battle:map-event-7", source.ResolveScene(battle, null, null, false).Context);
        Assert.Equal("scene:settlement-id|location-id", source.ResolveScene(null, "settlement-id|location-id", null, false).Context);
        var tournament = new TournamentMissionSession(controller);
        tournament.TryApplyState("tournament", "tournament-mission", 1, 1, "match", "host", new[] { "host" });
        Assert.Equal("scene:tournament-mission", source.ResolveScene(null, null, tournament, false).Context);
    }

    [Fact]
    public void TournamentSpectatorTransitionRetainsInstanceAndExitClearsContext()
    {
        var source = new VoiceSceneSource();
        var tournament = new TournamentMissionSession(Mock.Of<IControllerIdProvider>());
        tournament.TryApplyState("tournament", "tournament-mission", 1, 1, "match", "host", new[] { "host" });
        var participant = source.ResolveScene(null, null, tournament, false);
        var spectator = source.ResolveScene(null, null, tournament, true);
        Assert.Equal(participant.Context, spectator.Context);
        Assert.False(participant.IsSpectator);
        Assert.True(spectator.IsSpectator);
        tournament.Reset();
        Assert.Null(source.ResolveScene(null, null, tournament, true).Context);
        Assert.Equal("scene:town|tavern", source.ResolveScene(null, "town|tavern", tournament, true).Context);
    }
}
