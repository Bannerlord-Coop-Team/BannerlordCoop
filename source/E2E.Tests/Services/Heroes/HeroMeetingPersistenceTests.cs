using E2E.Tests.Util;
using GameInterface.CoopSessionData;
using GameInterface.Services.Players;
using GameInterface.Services.Players.Data;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.Settlements;
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

    [Fact]
    public void Client_FirstMeetingWithTownNotable_AppliesCalculatingRelationOnce()
    {
        const string controllerId = "PlayerOne";
        var client = Clients.First();
        var playerHeroId = TestEnvironment.CreateRegisteredObject<Hero>();
        var notableId = TestEnvironment.CreateRegisteredObject<Hero>();
        var settlementId = TestEnvironment.CreateRegisteredObject<Settlement>();
        var townId = TestEnvironment.CreateRegisteredObject<Town>();

        Server.Call(() =>
        {
            var playerManager = Server.Resolve<IPlayerManager>();
            Assert.True(playerManager.AddPlayer(
                new Player(controllerId, playerHeroId, string.Empty, string.Empty, string.Empty)));
            playerManager.SetPeer(controllerId, client.NetPeer);

            var playerHero = Server.GetRegisteredObject<Hero>(playerHeroId);
            var notable = Server.GetRegisteredObject<Hero>(notableId);
            var settlement = Server.GetRegisteredObject<Settlement>(settlementId);
            settlement.SetSettlementComponent(Server.GetRegisteredObject<Town>(townId));
            playerHero.Clan.SetLeader(playerHero);
            notable.Clan.SetLeader(notable);
            playerHero.SetTraitLevel(DefaultTraits.Calculating, 2);
            notable.SetNewOccupation(Occupation.Merchant);
            notable.StayingInSettlement = settlement;
            Assert.True(notable.IsNotable);
            Assert.True(notable.CurrentSettlement.IsTown);
            Assert.Equal(0, playerHero.GetRelation(notable));
        });

        for (var meeting = 0; meeting < 2; meeting++)
        {
            client.Call(() =>
            {
                Assert.True(client.ObjectManager.TryGetObject<Hero>(playerHeroId, out var playerHero));
                Assert.True(client.ObjectManager.TryGetObject<Hero>(notableId, out var notable));
                Game.Current.PlayerTroop = playerHero.CharacterObject;

                notable.SetHasMet();
            });
        }

        Server.Call(() =>
        {
            var playerHero = Server.GetRegisteredObject<Hero>(playerHeroId);
            var notable = Server.GetRegisteredObject<Hero>(notableId);
            // Calculating 2 is -6 relation with the notable, applied on the first meeting only.
            Assert.Equal(-6, playerHero.GetRelation(notable));
        });
    }
}
