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
using TaleWorlds.CampaignSystem.ComponentInterfaces;
using TaleWorlds.CampaignSystem.Naval;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
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
            playerManager.BindRegisteredPeer("leader", client.NetPeer);
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

        int goldBefore = 0;
        Server.Call(() =>
        {
            UseShipCostModel();
            goldBefore = Server.GetRegisteredObject<Hero>(mainHeroId).Gold;
        });

        Server.SimulateMessage(client.NetPeer, new ChangeEmptyClanPartyLeader(mainHeroId, leaderId, null));
        Server.PumpGameThread();

        // No clan party needs the ship, so the disband sells it to the clan leader at the selling penalty
        int expectedGold = goldBefore + (withShip ? (int)(TestShipCostModel.TradeValue * TestShipCostModel.SellingPenalty) : 0);
        Server.Call(() => Assert.Equal(expectedGold, Server.GetRegisteredObject<Hero>(mainHeroId).Gold));

        foreach (var instance in Clients.Prepend(Server))
        {
            instance.PumpGameThread();
            instance.Call(() => Assert.Empty(EmptyClanParties()));
        }
    }

    private static List<EmptyClanPartiesCampaignBehavior.EmptyClanParty> EmptyClanParties() =>
        Campaign.Current.GetCampaignBehavior<EmptyClanPartiesCampaignBehavior>()._emptyClanParties;

    // The harness Campaign boots without models, and the disband prices leftover ships with the ShipCostModel
    private void UseShipCostModel()
    {
        var models = (Campaign.Current.Models?.GetGameModels() ?? Enumerable.Empty<GameModel>())
            .Where(model => model is not ShipCostModel).ToList();
        models.Add(new TestShipCostModel());
        var gameModels = new GameModels(models);
        Server.GameInstance.Game._gameModelManagers[typeof(GameModels)] = gameModels;
        Campaign.Current._gameModels = gameModels;
    }

    private sealed class TestShipCostModel : ShipCostModel
    {
        public const float TradeValue = 1000f;
        public const float SellingPenalty = 0.5f;

        public override float GetShipTradeValue(Ship ship, PartyBase seller, PartyBase buyer) => TradeValue;
        public override float GetShipRepairCost(Ship ship, PartyBase owner) => 0f;
        public override int GetShipUpgradePieceCost(Ship ship, ShipUpgradePiece piece, PartyBase owner) => 0;
        public override float GetShipSellingPenalty() => SellingPenalty;
    }
}
