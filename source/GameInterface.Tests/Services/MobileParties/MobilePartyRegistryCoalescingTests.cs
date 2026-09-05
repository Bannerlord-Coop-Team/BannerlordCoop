using Common;
using Common.Messaging;
using Common.Network;
using Common.Network.Coalescing;
using Common.Util;
using Coop.Tests.Mocks;
using GameInterface.Registry.Auto;
using GameInterface.Services.Entity;
using GameInterface.Services.ItemRosters;
using GameInterface.Services.MobileParties;
using GameInterface.Services.MobileParties.Messages;
using GameInterface.Services.TroopRosters;
using Moq;
using Serilog;
using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using Xunit;
using ObjectManagerService = GameInterface.Services.ObjectManager.ObjectManager;

namespace GameInterface.Tests.Services.MobileParties;

public sealed class MobilePartyRegistryCoalescingTests
{
    private const string ItemRosterId = "ItemRoster_destroy-me";
    private const string MemberRosterId = "TroopRoster_destroy-me-members";
    private const string PrisonRosterId = "TroopRoster_destroy-me-prisoners";

    [Fact]
    public void Destroy_DropsPendingBehaviorBeforeNetworkDestroy_AndFlushHasNoLateUpdate()
    {
        const string fullPartyId = "MobileParty_destroy-me";
        const string compactPartyId = "destroy-me";

        var party = CreateParty();

        var objectManager = new ObjectManagerService(Mock.Of<ILogger>());
        Assert.True(objectManager.AddExisting(fullPartyId, party));

        var coalescer = new SendCoalescer();
        coalescer.Enqueue(
            new CoalesceKey("party-behavior", compactPartyId),
            new LatestWinsPayload(new PendingBehaviorUpdate()));

        var sent = new List<IMessage>();
        var network = new Mock<INetwork>();
        network
            .Setup(instance => instance.SendAll(It.IsAny<IMessage>()))
            .Callback<IMessage>(sent.Add);

        var broker = new MessageBroker();
        bool mobilePartyDestroyedPublished = false;
        Action<MessagePayload<MobilePartyDestroyed>> destroyedSubscription =
            _ => mobilePartyDestroyedPublished = true;
        broker.Subscribe(destroyedSubscription);

        var registry = new MobilePartyRegistry(
            Mock.Of<IControllerIdProvider>(),
            broker,
            Mock.Of<ILogger>(),
            Mock.Of<IAutoRegistryFactory>(),
            objectManager,
            coalescer);
        using var handler = new AutoRegistryHandler<MobileParty>(
            registry,
            broker,
            network.Object,
            objectManager);

        broker.Publish(this, new InstanceDestroyed<MobileParty>(party));

        Assert.True(mobilePartyDestroyedPublished);
        Assert.False(coalescer.HasPending);
        var destroy = Assert.IsType<NetworkDestroyInstance<MobileParty>>(Assert.Single(sent));
        Assert.Equal(fullPartyId, destroy.InstanceId);

        coalescer.Flush(network.Object);
        Assert.Single(sent);
    }

    [Fact]
    public void Destroy_RemovesRosterIdsOnServerAndClient()
    {
        var serverParty = CreateParty();
        var clientParty = CreateParty();
        var serverObjectManager = new ObjectManagerService(Mock.Of<ILogger>());
        var clientObjectManager = new ObjectManagerService(Mock.Of<ILogger>());
        Assert.True(serverObjectManager.AddExisting("MobileParty_destroy-me", serverParty));
        RegisterRosters(serverObjectManager, serverParty);
        RegisterRosters(clientObjectManager, clientParty);

        var serverBroker = new MessageBroker();
        var clientBroker = new MessageBroker();
        using var serverNetwork = new TestNetwork();
        var clientPeer = serverNetwork.CreatePeer();
        var clientNetwork = Mock.Of<INetwork>();
        var logger = Mock.Of<ILogger>();
        var factory = Mock.Of<IAutoRegistryFactory>();

        using var serverMobilePartyHandler = new AutoRegistryHandler<MobileParty>(
            new MobilePartyRegistry(Mock.Of<IControllerIdProvider>(), serverBroker, logger, factory, serverObjectManager),
            serverBroker, serverNetwork, serverObjectManager);
        using var serverItemRosterHandler = new AutoRegistryHandler<ItemRoster>(
            new ItemRosterRegistry(logger, factory, serverObjectManager),
            serverBroker, serverNetwork, serverObjectManager);
        using var serverTroopRosterHandler = new AutoRegistryHandler<TroopRoster>(
            new TroopRosterRegistry(logger, factory, serverObjectManager),
            serverBroker, serverNetwork, serverObjectManager);
        using var clientItemRosterHandler = new AutoRegistryHandler<ItemRoster>(
            new ItemRosterRegistry(logger, factory, clientObjectManager),
            clientBroker, clientNetwork, clientObjectManager);
        using var clientTroopRosterHandler = new AutoRegistryHandler<TroopRoster>(
            new TroopRosterRegistry(logger, factory, clientObjectManager),
            clientBroker, clientNetwork, clientObjectManager);

        serverBroker.Publish(this, new InstanceDestroyed<MobileParty>(serverParty));

        foreach (var message in serverNetwork.GetPeerMessagesFromType<NetworkDestroyInstance<ItemRoster>>(clientPeer))
            clientBroker.Publish(this, message);
        foreach (var message in serverNetwork.GetPeerMessagesFromType<NetworkDestroyInstance<TroopRoster>>(clientPeer))
            clientBroker.Publish(this, message);
        GameThread.Run(() => { }, blocking: true);

        AssertRostersRemoved(serverObjectManager);
        AssertRostersRemoved(clientObjectManager);
    }

    private static MobileParty CreateParty()
    {
        var party = ObjectHelper.SkipConstructor<MobileParty>();
        party.Party = ObjectHelper.SkipConstructor<PartyBase>();
        party.Party.MobileParty = party;
        party.Party.ItemRoster = new ItemRoster();
        party.Party.MemberRoster = new TroopRoster();
        party.Party.PrisonRoster = new TroopRoster();
        return party;
    }

    private static void RegisterRosters(ObjectManagerService objectManager, MobileParty party)
    {
        Assert.True(objectManager.AddExisting(ItemRosterId, party.ItemRoster));
        Assert.True(objectManager.AddExisting(MemberRosterId, party.MemberRoster));
        Assert.True(objectManager.AddExisting(PrisonRosterId, party.PrisonRoster));
    }

    private static void AssertRostersRemoved(ObjectManagerService objectManager)
    {
        Assert.False(objectManager.Contains(ItemRosterId));
        Assert.False(objectManager.Contains(MemberRosterId));
        Assert.False(objectManager.Contains(PrisonRosterId));
    }

    private readonly struct PendingBehaviorUpdate : ICommand
    {
    }
}
