using E2E.Tests.Util;
using GameInterface.CoopSessionData;
using GameInterface.Services.Players;
using GameInterface.Services.Players.Data;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Heroes;

/// <summary>
/// Verifies client hero meetings are persisted by the server.
/// </summary>
public class HeroMeetingPersistenceTests : SyncTestBase
{
    public HeroMeetingPersistenceTests(ITestOutputHelper output) : base(output)
    {
    }

    [Fact]
    public void Client_SetHasMet_PersistsMeetingOnServer()
    {
        const string controllerId = "PlayerOne";
        var client = Clients.First();
        var playerHeroId = TestEnvironment.CreateRegisteredObject<Hero>();
        var metHeroId = TestEnvironment.CreateRegisteredObject<Hero>();
        long? expectedMeetingTimeTicks = null;

        Server.Call(() =>
        {
            var playerManager = Server.Resolve<IPlayerManager>();
            Assert.True(playerManager.AddPlayer(
                new Player(controllerId, playerHeroId, string.Empty, string.Empty, string.Empty)));
            playerManager.SetPeer(controllerId, client.NetPeer);
        });

        client.Call(() =>
        {
            Assert.True(client.ObjectManager.TryGetObject<Hero>(playerHeroId, out var playerHero));
            Assert.True(client.ObjectManager.TryGetObject<Hero>(metHeroId, out var metHero));
            Game.Current.PlayerTroop = playerHero.CharacterObject;

            metHero.SetHasMet();
            expectedMeetingTimeTicks = metHero.LastMeetingTimeWithPlayer._numTicks;
        });

        Assert.True(expectedMeetingTimeTicks.HasValue);
        Server.Call(() =>
        {
            var meetings = Server.Resolve<ICoopSessionProvider>()
                .CoopSession.HeroMeetingData.PlayerLastMeetingTimes;
            Assert.True(meetings.TryGetValue(playerHeroId, out var playerMeetings));
            Assert.True(playerMeetings.TryGetValue(metHeroId, out var lastMeetingTimeTicks));
            Assert.Equal(expectedMeetingTimeTicks.Value, lastMeetingTimeTicks);
        });
    }
}
