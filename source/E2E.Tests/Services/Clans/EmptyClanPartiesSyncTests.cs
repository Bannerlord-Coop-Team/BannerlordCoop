using Common.Messaging;
using Common.Util;
using E2E.Tests.Environment.Instance;
using E2E.Tests.Util;
using GameInterface.Services.Clans.Interfaces;
using GameInterface.Services.Clans.Messages;
using GameInterface.Services.Players;
using GameInterface.Services.Players.Data;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Naval;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Clans;

/// <summary>
/// The server owns the cached empty clan parties, and every client keeps the same leaders and ships for its clan screen.
/// </summary>
public class EmptyClanPartiesSyncTests : SyncTestBase
{
    private readonly string clanId;
    private readonly string mainHeroId;
    private readonly string leaderId;
    private readonly EnvironmentInstance client;

    public EmptyClanPartiesSyncTests(ITestOutputHelper output) : base(output)
    {
        clanId = TestEnvironment.CreateRegisteredObject<Clan>();
        mainHeroId = TestEnvironment.CreateRegisteredObject<Hero>();
        leaderId = TestEnvironment.CreateRegisteredObject<Hero>();
        client = Clients.First();

        foreach (var instance in Clients.Prepend(Server))
        {
            instance.Call(() =>
            {
                Campaign.Current.AddCampaignBehaviorManager(new CampaignBehaviorManager(
                    new CampaignBehaviorBase[] { new EmptyClanPartiesCampaignBehavior() }));

                var clan = instance.GetRegisteredObject<Clan>(clanId);
                var mainHero = instance.GetRegisteredObject<Hero>(mainHeroId);
                using (new AllowedThread())
                {
                    mainHero.Clan = clan;
                    instance.GetRegisteredObject<Hero>(leaderId).Clan = clan;
                    clan.SetLeader(mainHero);
                }
            });
        }

        Server.Call(() =>
        {
            var playerManager = Server.Resolve<IPlayerManager>();
            Assert.True(playerManager.AddPlayer(new Player("leader", mainHeroId, null, clanId, null)));
            playerManager.SetPeer("leader", client.NetPeer);
        });
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void AddedThenDisbanded_ReachesEveryClient(bool withShip)
    {
        string? shipId = withShip ? TestEnvironment.CreateRegisteredObject<Ship>() : null;

        Server.Call(() =>
        {
            var leader = Server.GetRegisteredObject<Hero>(leaderId);
            var ships = shipId == null ? new List<Ship>() : new List<Ship> { Server.GetRegisteredObject<Ship>(shipId) };

            // Mirrors OnMobilePartyDestroyed, which adds the entry and announces it
            Server.Resolve<IEmptyClanPartiesCampaignBehaviorInterface>().AddEmptyClanParty(leader, ships);
            Server.Resolve<IMessageBroker>().Publish(this, new EmptyClanPartyAdded(leader, ships.ToArray()));
        });

        foreach (var instance in Clients)
        {
            instance.PumpGameThread();
            instance.Call(() =>
            {
                var entry = Assert.Single(EmptyClanParties());
                Assert.Same(instance.GetRegisteredObject<Hero>(leaderId), entry.Leader);
                if (shipId == null) Assert.Empty(entry.Ships);
                else Assert.Same(instance.GetRegisteredObject<Ship>(shipId), Assert.Single(entry.Ships));
            });
        }

        Server.SimulateMessage(client.NetPeer, new ChangeEmptyClanPartyLeader(mainHeroId, leaderId, null));
        Server.PumpGameThread();

        foreach (var instance in Clients.Prepend(Server))
        {
            instance.PumpGameThread();
            instance.Call(() => Assert.Empty(EmptyClanParties()));
        }
    }

    private static List<EmptyClanPartiesCampaignBehavior.EmptyClanParty> EmptyClanParties() =>
        Campaign.Current.GetCampaignBehavior<EmptyClanPartiesCampaignBehavior>()._emptyClanParties;
}
