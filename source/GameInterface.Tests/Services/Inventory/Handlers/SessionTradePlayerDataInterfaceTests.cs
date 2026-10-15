using Autofac;
using Common.Messaging;
using Common.Network;
using Common.Network.Coalescing;
using Common.Util;
using Coop.Core.Server.Services.ItemRosters.Messages;
using GameInterface.CoopSessionData;
using GameInterface.Services.Inventory.TradeSkills.Handlers;
using GameInterface.Services.Inventory.TradeSkills.Interfaces;
using GameInterface.Services.Inventory.TradeSkills.Messages;
using GameInterface.Services.ObjectManager;
using Moq;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.Core;
using Xunit;

namespace GameInterface.Tests.Services.Inventory.Handlers;

public class SessionTradePlayerDataInterfaceTests
{
    [Fact]
    public void ClientHandler_ResolvesWithoutServerCoalescer()
    {
        var builder = new ContainerBuilder();
        builder.RegisterInstance(Mock.Of<ICoopSessionProvider>());
        builder.RegisterInstance(Mock.Of<IObjectManager>());
        builder.RegisterInstance(Mock.Of<INetwork>());
        builder.RegisterInstance(Mock.Of<IMessageBroker>());
        builder.RegisterType<SessionTradePlayerDataInterface>().As<ISessionTradePlayerDataInterface>();
        builder.RegisterType<TradeSkillHandler>().AsSelf().AutoActivate();

        using var container = builder.Build();

        Assert.False(container.IsRegistered<ISendCoalescer>());
        Assert.NotNull(container.Resolve<TradeSkillHandler>());
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Completion_FlushesRegisteredOwnerRosterAndAlwaysSendsReceipt(bool registered)
    {
        var hero = ObjectHelper.SkipConstructor<Hero>();
        var party = ObjectHelper.SkipConstructor<MobileParty>();
        party.Party = ObjectHelper.SkipConstructor<PartyBase>();
        party.Party.ItemRoster = new ItemRoster();
        hero._partyBelongedTo = party;
        string heroId = "owner";
        uint rosterHandle = 17;
        var objects = new Mock<IObjectManager>(MockBehavior.Strict);
        objects.Setup(o => o.TryGetIdWithLogging(hero, out heroId)).Returns(true);
        objects.Setup(o => o.TryGetHandleWithLogging(party.ItemRoster, out rosterHandle)).Returns(registered);
        var sent = new List<IMessage>();
        var network = new Mock<INetwork>();
        network.Setup(n => n.SendAll(It.IsAny<IMessage>())).Callback<IMessage>(sent.Add);
        var coalescer = new SendCoalescer();
        if (registered)
        {
            coalescer.Enqueue(new CoalesceKey("ItemRosterUpdate", rosterHandle, "3:0"),
                new SummedPayload<int>(2, (a, b) => a + b, amount => new NetworkItemRosterUpdate(rosterHandle, 3, 0, amount)));
        }
        coalescer.Enqueue(new CoalesceKey("ItemRosterUpdate", 18u, "3:0"),
            new SummedPayload<int>(1, (a, b) => a + b, amount => new NetworkItemRosterUpdate(18, 3, 0, amount)));
        var service = new SessionTradePlayerDataInterface(Mock.Of<ICoopSessionProvider>(), objects.Object, network.Object, coalescer);

        service.UpdatePlayerInventory(hero, new List<(ItemRosterElement, int)>(), new List<(ItemRosterElement, int)>(), false);

        Assert.Equal(registered ? 2 : 1, sent.Count);
        if (registered) Assert.IsType<NetworkItemRosterUpdate>(sent[0]);
        var completion = Assert.IsType<NetworkUpdateTradeData>(sent[sent.Count - 1]);
        Assert.Equal(heroId, completion.PlayerHeroId);
        Assert.False(completion.IsTrading);
        Assert.True(coalescer.HasPending);
        objects.VerifyAll();
    }

    [Fact]
    public void EmptyNonTradingCompletionWithoutParty_PreservesExistingReceipt()
    {
        var hero = ObjectHelper.SkipConstructor<Hero>();
        string heroId = "owner";
        var objects = new Mock<IObjectManager>(MockBehavior.Strict);
        objects.Setup(o => o.TryGetIdWithLogging(hero, out heroId)).Returns(true);
        var network = new Mock<INetwork>();
        var service = new SessionTradePlayerDataInterface(Mock.Of<ICoopSessionProvider>(), objects.Object, network.Object);

        service.UpdatePlayerInventory(hero, new List<(ItemRosterElement, int)>(), new List<(ItemRosterElement, int)>(), false);

        network.Verify(n => n.SendAll(It.Is<NetworkUpdateTradeData>(m => m.PlayerHeroId == heroId)), Times.Once);
        objects.VerifyAll();
    }
}
